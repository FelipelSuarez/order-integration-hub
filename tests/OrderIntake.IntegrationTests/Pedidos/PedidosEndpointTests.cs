using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Api.Pedidos;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;

namespace OrderIntake.IntegrationTests.Pedidos;

[Collection(nameof(SqlServerCollection))]
public sealed class PedidosEndpointTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task PostPedidos_ComPayloadValido_Retorna202EPersisteOPedido()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegistrarPedidoRequest(Guid.NewGuid(), [new ItemRequest(Guid.NewGuid(), 2)]);

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<RegistrarPedidoResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Recebido");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderIntakeDbContext>();
        var persistido = await context.Pedidos.FindAsync(body.PedidoId);

        persistido.Should().NotBeNull();
    }

    [Fact]
    public async Task PostPedidos_SemItens_Retorna400()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegistrarPedidoRequest(Guid.NewGuid(), []);

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
