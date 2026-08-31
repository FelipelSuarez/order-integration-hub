using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Api.Pedidos;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;
using OrderIntake.IntegrationTests.Legado;

namespace OrderIntake.IntegrationTests.Pedidos;

[Collection(nameof(IntegrationCollection))]
public sealed class PedidosEndpointTests(SqlServerContainerFixture fixture, RabbitMqContainerFixture rabbitMqFixture, LegadoFakeHostFixture legadoFake)
    : IAsyncLifetime
{
    // Defensivo: LegadoFakeHostFixture é compartilhado por toda a IntegrationCollection
    // (não por classe) — garante que este teste não herde um toggle de indisponibilidade
    // deixado ligado por outra classe.
    public Task InitializeAsync() => legadoFake.SimularIndisponibilidadeAsync(ativo: false);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostPedidos_ComPayloadValido_Retorna202EPersisteOPedido()
    {
        // Endereço real do legado (não o default localhost:5236, que precisa de nada
        // escutando pra sequer existir): sem isso, a saga precisa esgotar retry+timeout
        // do Polly contra uma porta fechada antes de Recebido→Validando ficar visível —
        // rápido isolado, mas some no timeout de 120s sob a contenção da suíte inteira.
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString, rabbitMqFixture.ConnectionString, legadoFake.EnderecoServico);
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

        // Espera o outbox entregar antes de sair: a entrega é assíncrona (background), e
        // descartar o factory antes dela terminar derruba o host no meio do processamento
        // da própria mensagem que este teste acabou de gerar — ObjectDisposedException no
        // consumer, não neste teste (que já passou), mas potencialmente noutro teste que
        // dependa da mesma fila.
        await PedidoAguardo.AguardarStatusAsync(factory, body.PedidoId, Status.Validando);
    }

    [Fact]
    public async Task PostPedidos_SemItens_Retorna400()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString, rabbitMqFixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegistrarPedidoRequest(Guid.NewGuid(), []);

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostPedidos_ComItensNulo_Retorna400EmVezDe500()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString, rabbitMqFixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegistrarPedidoRequest(Guid.NewGuid(), null!);

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
