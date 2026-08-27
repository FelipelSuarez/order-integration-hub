using System.Net.Http.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Api.Pedidos;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;
using Shared.Contracts.Pedidos.V1;

namespace OrderIntake.IntegrationTests.Pedidos;

/// <summary>
/// Prova o pipeline de verdade — outbox (ADR-0003) → RabbitMQ real (Testcontainers) →
/// PedidoRecebidoConsumer — e a idempotência dele (ADR-0007): reentrega do mesmo
/// PedidoRecebido não duplica a transição de estado.
/// </summary>
[Collection(nameof(IntegrationCollection))]
public sealed class PedidoRecebidoMessagingTests(SqlServerContainerFixture fixture, RabbitMqContainerFixture rabbitMqFixture)
{
    [Fact]
    public async Task PedidoRecebido_PublicadoPeloOutbox_ChegaAoConsumerEViraValidando()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString, rabbitMqFixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegistrarPedidoRequest(Guid.NewGuid(), [new ItemRequest(Guid.NewGuid(), 2)]);
        var response = await client.PostAsJsonAsync("/pedidos", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<RegistrarPedidoResponse>();
        body.Should().NotBeNull();

        var pedido = await AguardarStatusAsync(factory, body!.PedidoId, Status.Validando);
        pedido.Status.Should().Be(Status.Validando);

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        // Observa o consumo de verdade da reentrega — sem isso, o teste passaria mesmo
        // que o consumer não estivesse rodando, já que o Status já é Validando.
        var reentregaConsumida = new PedidoRecebidoConsumeObserver(pedido.Id);
        using var observerHandle = bus.ConnectConsumeObserver(reentregaConsumida);

        // Publica direto no IBus, não no IPublishEndpoint resolvido por escopo: esse
        // último é o outbox-aware (ADR-0003) e só entrega quando SaveChangesAsync roda
        // no DbContext do mesmo escopo — não é o caso aqui, e não é o que se quer
        // simular (redelivery é o broker reentregando, não a aplicação republicando).
        await bus.Publish(new PedidoRecebido(pedido.Id, pedido.ClienteId, DateTimeOffset.UtcNow));

        await reentregaConsumida.WaitAsync(TimeSpan.FromSeconds(30));

        var pedidoAposReentrega = await ObterPedidoAsync(factory, pedido.Id);
        pedidoAposReentrega!.Status.Should().Be(Status.Validando);
    }

    private static async Task<Pedido> AguardarStatusAsync(OrderIntakeApiFactory factory, Guid pedidoId, Status statusEsperado)
    {
        // 60s: folga generosa pra primeira execução numa máquina limpa, onde a imagem do
        // RabbitMQ ainda não está em cache local e o pull compete com o resto da suíte.
        var limite = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < limite)
        {
            var pedido = await ObterPedidoAsync(factory, pedidoId);

            if (pedido?.Status == statusEsperado)
            {
                return pedido;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Pedido {pedidoId} não chegou a {statusEsperado} a tempo.");
    }

    private static async Task<Pedido?> ObterPedidoAsync(OrderIntakeApiFactory factory, Guid pedidoId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderIntakeDbContext>();

        return await context.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pedidoId);
    }

    /// <summary>
    /// Sinaliza quando o broker efetivamente entrega e o consumer processa (com sucesso
    /// ou não) o PedidoRecebido do PedidoId observado — prova consumo real da reentrega,
    /// não apenas ausência de mudança de estado.
    /// </summary>
    private sealed class PedidoRecebidoConsumeObserver(Guid pedidoId) : IConsumeObserver
    {
        private readonly TaskCompletionSource _consumido = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            await using (cts.Token.Register(() => _consumido.TrySetException(
                new TimeoutException($"Reentrega de PedidoRecebido para {pedidoId} não foi consumida a tempo."))))
            {
                await _consumido.Task;
            }
        }

        public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

        public Task PostConsume<T>(ConsumeContext<T> context) where T : class
        {
            if (context.Message is PedidoRecebido mensagem && mensagem.PedidoId == pedidoId)
            {
                _consumido.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
        {
            if (context.Message is PedidoRecebido mensagem && mensagem.PedidoId == pedidoId)
            {
                _consumido.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }
}
