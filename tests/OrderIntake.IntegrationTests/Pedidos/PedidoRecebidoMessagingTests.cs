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
        var body = await response.Content.ReadFromJsonAsync<RegistrarPedidoResponse>();

        var pedido = await AguardarStatusAsync(factory, body!.PedidoId, Status.Validando);
        pedido.Status.Should().Be(Status.Validando);

        using var scope = factory.Services.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(new PedidoRecebido(pedido.Id, pedido.ClienteId, DateTimeOffset.UtcNow));

        // Dá tempo do consumer reprocessar a reentrega antes de reafirmar o estado.
        await Task.Delay(TimeSpan.FromSeconds(2));

        var pedidoAposReentrega = await ObterPedidoAsync(factory, pedido.Id);
        pedidoAposReentrega!.Status.Should().Be(Status.Validando);
    }

    private static async Task<Pedido> AguardarStatusAsync(OrderIntakeApiFactory factory, Guid pedidoId, Status statusEsperado)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);

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
}
