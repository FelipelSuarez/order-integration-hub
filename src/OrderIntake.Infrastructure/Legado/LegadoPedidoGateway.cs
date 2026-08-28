using System.ServiceModel;
using OrderIntake.Application.Pedidos;
using OrderIntake.Infrastructure.Legado.Gerado;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace OrderIntake.Infrastructure.Legado;

/// <summary>
/// Adapter da porta <see cref="ILegadoPedidoGateway"/>: fala SOAP com o cliente gerado via
/// dotnet-svcutil (Legado/Gerado, nunca editado à mão) e envolve a chamada num pipeline
/// Polly (retry com backoff, circuit breaker e timeout por tentativa — ADR-0006). Nenhum
/// tipo de System.ServiceModel atravessa para fora desta classe: sucesso e recusa viram
/// <see cref="ResultadoLegado"/>, falha técnica vira <see cref="LegadoIndisponivelException"/>.
/// </summary>
public sealed class LegadoPedidoGateway(string enderecoServico) : ILegadoPedidoGateway
{
    private readonly ResiliencePipeline<ValidarEReservarPedidoResponse> _pipeline = ConstruirPipeline();

    public async Task<ResultadoLegado> ValidarEReservarAsync(
        Guid clienteId,
        IReadOnlyCollection<(Guid ProdutoId, int Quantidade)> itens,
        CancellationToken cancellationToken)
    {
        var request = new ValidarEReservarPedidoRequest
        {
            ClienteId = clienteId,
            Itens = itens
                .Select(item => new ItemPedidoLegado { ProdutoId = item.ProdutoId, Quantidade = item.Quantidade })
                .ToArray(),
        };

        try
        {
            var response = await _pipeline.ExecuteAsync(
                (ct) => new ValueTask<ValidarEReservarPedidoResponse>(ChamarLegadoAsync(enderecoServico, request, ct)),
                cancellationToken);

            return response.Aprovado
                ? new ResultadoLegado.Aprovado()
                : new ResultadoLegado.Recusado(response.Motivo ?? "Legado recusou o pedido sem informar motivo.");
        }
        catch (Exception ex) when (EhFalhaTecnica(ex))
        {
            throw new LegadoIndisponivelException(
                "Legado indisponível: circuito aberto ou falha técnica persistente na chamada SOAP.", ex);
        }
    }

    private static async Task<ValidarEReservarPedidoResponse> ChamarLegadoAsync(
        string enderecoServico, ValidarEReservarPedidoRequest request, CancellationToken cancellationToken)
    {
        var client = new ServicoLegadoClient(
            ServicoLegadoClient.EndpointConfiguration.BasicHttpBinding_IServicoLegado, enderecoServico);

        try
        {
            return await client.ValidarEReservarPedidoAsync(request).WaitAsync(cancellationToken);
        }
        finally
        {
            await FecharAsync(client);
        }
    }

    private static async Task FecharAsync(ServicoLegadoClient client)
    {
        try
        {
            await client.CloseAsync();
        }
        catch (CommunicationException)
        {
            client.Abort();
        }
        catch (TimeoutException)
        {
            client.Abort();
        }
    }

    private static bool EhFalhaTecnica(Exception ex) =>
        ex is BrokenCircuitException or FaultException or CommunicationException or TimeoutException or TimeoutRejectedException;

    private static ResiliencePipeline<ValidarEReservarPedidoResponse> ConstruirPipeline()
    {
        var falhaTecnica = new PredicateBuilder<ValidarEReservarPedidoResponse>()
            .Handle<FaultException>()
            .Handle<CommunicationException>()
            .Handle<TimeoutException>();

        return new ResiliencePipelineBuilder<ValidarEReservarPedidoResponse>()
            .AddRetry(new RetryStrategyOptions<ValidarEReservarPedidoResponse>
            {
                ShouldHandle = falhaTecnica,
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ValidarEReservarPedidoResponse>
            {
                ShouldHandle = falhaTecnica,
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(15),
            })
            .AddTimeout(TimeSpan.FromSeconds(2))
            .Build();
    }
}
