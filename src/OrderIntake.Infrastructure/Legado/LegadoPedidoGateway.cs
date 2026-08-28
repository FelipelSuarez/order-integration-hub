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
public sealed class LegadoPedidoGateway : ILegadoPedidoGateway
{
    private readonly string _enderecoServico;
    private readonly CircuitBreakerStateProvider _estadoCircuito = new();
    private readonly ResiliencePipeline<ValidarEReservarPedidoResponse> _pipeline;

    public LegadoPedidoGateway(string enderecoServico, LegadoResiliencePipelineOptions? opcoesResiliencia = null)
    {
        _enderecoServico = enderecoServico;
        _pipeline = ConstruirPipeline(opcoesResiliencia ?? LegadoResiliencePipelineOptions.Padrao, _estadoCircuito);
    }

    /// <summary>Exposto só para observação em teste — não faz parte da porta da Application.</summary>
    public CircuitState CircuitState => _estadoCircuito.CircuitState;

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
                (ct) => new ValueTask<ValidarEReservarPedidoResponse>(ChamarLegadoAsync(_enderecoServico, request, ct)),
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
            // Abort (não CloseAsync): BasicHttpBinding não tem sessão e o cliente é
            // descartado após esta chamada — negociar um close gracioso só arrisca travar
            // dentro da janela de timeout/circuit breaker do Polly sem nenhum ganho real.
            client.Abort();
        }
    }

    private static bool EhFalhaTecnica(Exception ex) =>
        ex is BrokenCircuitException or FaultException or CommunicationException or TimeoutException or TimeoutRejectedException;

    private static ResiliencePipeline<ValidarEReservarPedidoResponse> ConstruirPipeline(
        LegadoResiliencePipelineOptions opcoes, CircuitBreakerStateProvider estadoCircuito)
    {
        var falhaTecnica = new PredicateBuilder<ValidarEReservarPedidoResponse>()
            .Handle<FaultException>()
            .Handle<CommunicationException>()
            .Handle<TimeoutException>();

        return new ResiliencePipelineBuilder<ValidarEReservarPedidoResponse>()
            .AddRetry(new RetryStrategyOptions<ValidarEReservarPedidoResponse>
            {
                ShouldHandle = falhaTecnica,
                MaxRetryAttempts = opcoes.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = opcoes.RetryDelay,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ValidarEReservarPedidoResponse>
            {
                ShouldHandle = falhaTecnica,
                FailureRatio = opcoes.FailureRatio,
                MinimumThroughput = opcoes.MinimumThroughput,
                SamplingDuration = opcoes.SamplingDuration,
                BreakDuration = opcoes.BreakDuration,
                StateProvider = estadoCircuito,
            })
            .AddTimeout(opcoes.TimeoutPorTentativa)
            .Build();
    }
}
