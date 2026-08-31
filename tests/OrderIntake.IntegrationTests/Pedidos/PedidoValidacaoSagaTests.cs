using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Api.Pedidos;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Sagas;
using OrderIntake.IntegrationTests.Infrastructure;
using OrderIntake.IntegrationTests.Legado;

namespace OrderIntake.IntegrationTests.Pedidos;

/// <summary>
/// Prova a saga de verdade — outbox → RabbitMQ real → PedidoValidacaoStateMachine →
/// LegadoErp.Fake real (ADR-0011): aprovação, recusa de negócio, e o ponto central do
/// ticket, a saga sobrevivendo ao legado indisponível sem rejeitar o Pedido na hora.
/// </summary>
[Collection(nameof(IntegrationCollection))]
public sealed class PedidoValidacaoSagaTests : IAsyncLifetime
{
    private readonly SqlServerContainerFixture _sqlFixture;
    private readonly RabbitMqContainerFixture _rabbitMqFixture;
    private readonly LegadoFakeHostFixture _legadoFake;

    public PedidoValidacaoSagaTests(
        SqlServerContainerFixture sqlFixture, RabbitMqContainerFixture rabbitMqFixture, LegadoFakeHostFixture legadoFake)
    {
        _sqlFixture = sqlFixture;
        _rabbitMqFixture = rabbitMqFixture;
        _legadoFake = legadoFake;
    }

    public Task InitializeAsync() => _legadoFake.SimularIndisponibilidadeAsync(ativo: false);

    public Task DisposeAsync() => Task.CompletedTask;

    private OrderIntakeApiFactory CriarFactory(PedidoSagaOptions? opcoes = null) =>
        new(_sqlFixture.ConnectionString, _rabbitMqFixture.ConnectionString, _legadoFake.EnderecoServico, opcoes);

    [Fact]
    public async Task LegadoAprova_PedidoTerminaReservado()
    {
        await using var factory = CriarFactory();
        using var client = factory.CreateClient();

        var pedidoId = await RegistrarPedidoAsync(client, quantidade: 5);

        var pedido = await PedidoAguardo.AguardarStatusAsync(factory, pedidoId, Status.Reservado);

        pedido.Status.Should().Be(Status.Reservado);
    }

    [Fact]
    public async Task LegadoRecusaPorEstoque_PedidoTerminaRejeitadoSemRetentativa()
    {
        await using var factory = CriarFactory();
        using var client = factory.CreateClient();

        var pedidoId = await RegistrarPedidoAsync(client, quantidade: 999);

        var pedido = await PedidoAguardo.AguardarStatusAsync(factory, pedidoId, Status.Rejeitado);

        pedido.MotivoRejeicao.Should().Contain("Estoque insuficiente");
    }

    [Fact]
    public async Task LegadoIndisponivel_PedidoFicaEmValidando_ERecuperaQuandoLegadoVoltaAoAr()
    {
        var opcoes = new PedidoSagaOptions
        {
            IntervaloRetentativa = TimeSpan.FromSeconds(1),
            OrcamentoTotal = TimeSpan.FromSeconds(30),
        };
        await using var factory = CriarFactory(opcoes);
        using var client = factory.CreateClient();

        await _legadoFake.SimularIndisponibilidadeAsync(ativo: true);

        var pedidoId = await RegistrarPedidoAsync(client, quantidade: 5);

        // Dá tempo pra primeira tentativa falhar e confirma que a saga não rejeita na hora
        // — ela precisa continuar em Validando enquanto o legado está fora do ar.
        await Task.Delay(TimeSpan.FromSeconds(2));
        var pedidoEmValidando = await PedidoAguardo.ObterPedidoAsync(factory, pedidoId);
        pedidoEmValidando!.Status.Should().Be(Status.Validando);

        await _legadoFake.SimularIndisponibilidadeAsync(ativo: false);

        var pedidoFinal = await PedidoAguardo.AguardarStatusAsync(factory, pedidoId, Status.Reservado);
        pedidoFinal.Status.Should().Be(Status.Reservado);

        await EsperarReavaliacoesEmAbertoDrenaremAsync(opcoes);
    }

    [Fact]
    public async Task LegadoIndisponivelOTempoTodo_EstouraOrcamentoEVaiPraRejeitado()
    {
        var opcoes = new PedidoSagaOptions
        {
            IntervaloRetentativa = TimeSpan.FromMilliseconds(500),
            OrcamentoTotal = TimeSpan.FromSeconds(3),
        };
        await using var factory = CriarFactory(opcoes);
        using var client = factory.CreateClient();

        await _legadoFake.SimularIndisponibilidadeAsync(ativo: true);

        try
        {
            var pedidoId = await RegistrarPedidoAsync(client, quantidade: 5);

            var pedido = await PedidoAguardo.AguardarStatusAsync(factory, pedidoId, Status.Rejeitado);

            pedido.MotivoRejeicao.Should().Contain("orçamento");
            await EsperarReavaliacoesEmAbertoDrenaremAsync(opcoes);
        }
        finally
        {
            await _legadoFake.SimularIndisponibilidadeAsync(ativo: false);
        }
    }

    [Fact]
    public async Task ReavaliacaoAtrasadaAposResolvido_NaoChamaOLegadoDeNovo()
    {
        await using var factory = CriarFactory();
        using var client = factory.CreateClient();

        var pedidoId = await RegistrarPedidoAsync(client, quantidade: 5);
        var pedido = await PedidoAguardo.AguardarStatusAsync(factory, pedidoId, Status.Reservado);
        pedido.Status.Should().Be(Status.Reservado);

        var chamadasAposReservado = _legadoFake.ObterQuantidadeDeChamadas();

        // Reentrega (ADR-0007): simula uma reavaliação atrasada chegando depois do Pedido
        // já resolvido — DuringAny ignora sem tocar no legado de novo, em vez de estourar
        // "evento sem handler no estado atual" ou reprocessar a decisão já tomada.
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();
        await bus.Publish(new ReavaliarPedido(pedidoId));

        await Task.Delay(TimeSpan.FromSeconds(1));
        _legadoFake.ObterQuantidadeDeChamadas().Should().Be(chamadasAposReservado);

        var pedidoFinal = await PedidoAguardo.ObterPedidoAsync(factory, pedidoId);
        pedidoFinal!.Status.Should().Be(Status.Reservado);
    }

    private static async Task<Guid> RegistrarPedidoAsync(HttpClient client, int quantidade)
    {
        var request = new RegistrarPedidoRequest(Guid.NewGuid(), [new ItemRequest(Guid.NewGuid(), quantidade)]);
        var response = await client.PostAsJsonAsync("/pedidos", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<RegistrarPedidoResponse>();
        return body!.PedidoId;
    }

    /// <summary>
    /// Antes de descartar o factory: pode haver uma reavaliação já agendada
    /// (ScheduleSend) de uma tentativa anterior à que acabou de resolver o Pedido — a
    /// saga a ignora com segurança (DuringAny), mas só se o host ainda existir quando ela
    /// chegar. Sem essa espera, o host já descartado gera ObjectDisposedException no
    /// consumer da mensagem atrasada.
    /// </summary>
    private static Task EsperarReavaliacoesEmAbertoDrenaremAsync(PedidoSagaOptions opcoes) =>
        Task.Delay(opcoes.IntervaloRetentativa + TimeSpan.FromSeconds(1));
}
