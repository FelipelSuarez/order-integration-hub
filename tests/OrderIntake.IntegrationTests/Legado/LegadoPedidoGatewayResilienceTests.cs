using OrderIntake.Application.Pedidos;
using OrderIntake.Infrastructure.Legado;
using Polly.CircuitBreaker;

namespace OrderIntake.IntegrationTests.Legado;

/// <summary>
/// Contra o LegadoErp.Fake de verdade (sem mock) — prova o pipeline Polly do
/// LegadoPedidoGateway: aprovação, recusa de negócio (não é falha técnica) e o circuito
/// abrindo quando o legado fica indisponível (ZER-162).
/// </summary>
public sealed class LegadoPedidoGatewayResilienceTests : IClassFixture<LegadoFakeHostFixture>, IAsyncLifetime
{
    private readonly LegadoFakeHostFixture _legadoFake;

    public LegadoPedidoGatewayResilienceTests(LegadoFakeHostFixture legadoFake)
    {
        _legadoFake = legadoFake;
    }

    public Task InitializeAsync() => _legadoFake.SimularIndisponibilidadeAsync(ativo: false);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Timeout por tentativa bem folgado: os testes de caminho feliz não existem pra
    /// travar o valor de produção (esse fica documentado na ADR-0006), só pra provar
    /// aprovação/recusa sem ruído de uma chamada local eventualmente mais lenta.
    /// </summary>
    private LegadoPedidoGateway CriarGateway(LegadoResiliencePipelineOptions? opcoes = null) =>
        new(_legadoFake.EnderecoServico, opcoes ?? new LegadoResiliencePipelineOptions { TimeoutPorTentativa = TimeSpan.FromSeconds(10) });

    [Fact]
    public async Task LegadoDisponivel_PedidoValido_RetornaAprovado()
    {
        var gateway = CriarGateway();

        var resultado = await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 5)], CancellationToken.None);

        resultado.Should().BeOfType<ResultadoLegado.Aprovado>();
    }

    [Fact]
    public async Task LegadoDisponivel_EstoqueInsuficiente_RetornaRecusadoSemAbrirCircuito()
    {
        var gateway = CriarGateway();

        var resultado = await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 999)], CancellationToken.None);

        resultado.Should().BeOfType<ResultadoLegado.Recusado>()
            .Which.Motivo.Should().Contain("Estoque insuficiente");
        gateway.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task LegadoIndisponivel_FalhasSuperamOLimiar_AbreOCircuitoELancaLegadoIndisponivel()
    {
        var opcoes = new LegadoResiliencePipelineOptions
        {
            MaxRetryAttempts = 1,
            RetryDelay = TimeSpan.FromMilliseconds(10),
            TimeoutPorTentativa = TimeSpan.FromSeconds(2),
            FailureRatio = 0.5,
            MinimumThroughput = 2,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(5),
        };
        var gateway = CriarGateway(opcoes);
        await _legadoFake.SimularIndisponibilidadeAsync(ativo: true);

        var primeiraChamada = async () => await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 1)], CancellationToken.None);

        await primeiraChamada.Should().ThrowAsync<LegadoIndisponivelException>();
        gateway.CircuitState.Should().Be(CircuitState.Open);

        var chamadaComCircuitoAberto = async () => await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 1)], CancellationToken.None);

        (await chamadaComCircuitoAberto.Should().ThrowAsync<LegadoIndisponivelException>())
            .WithInnerException<BrokenCircuitException>();

        await _legadoFake.SimularIndisponibilidadeAsync(ativo: false);
    }

    [Fact]
    public async Task LegadoVoltaAoAr_AposBreakDuration_CircuitoFechaDeNovo()
    {
        var opcoes = new LegadoResiliencePipelineOptions
        {
            MaxRetryAttempts = 1,
            RetryDelay = TimeSpan.FromMilliseconds(10),
            TimeoutPorTentativa = TimeSpan.FromSeconds(2),
            FailureRatio = 0.5,
            MinimumThroughput = 2,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromMilliseconds(500),
        };
        var gateway = CriarGateway(opcoes);
        await _legadoFake.SimularIndisponibilidadeAsync(ativo: true);

        var chamadaComLegadoIndisponivel = async () => await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 1)], CancellationToken.None);
        await chamadaComLegadoIndisponivel.Should().ThrowAsync<LegadoIndisponivelException>();
        gateway.CircuitState.Should().Be(CircuitState.Open);

        await _legadoFake.SimularIndisponibilidadeAsync(ativo: false);
        await Task.Delay(opcoes.BreakDuration + TimeSpan.FromMilliseconds(100));

        var resultado = await gateway.ValidarEReservarAsync(
            Guid.NewGuid(), [(Guid.NewGuid(), 1)], CancellationToken.None);

        resultado.Should().BeOfType<ResultadoLegado.Aprovado>();
        gateway.CircuitState.Should().Be(CircuitState.Closed);
    }
}
