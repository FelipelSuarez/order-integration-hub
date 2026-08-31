namespace OrderIntake.Infrastructure.Legado;

/// <summary>
/// Parâmetros do pipeline Polly de <see cref="LegadoPedidoGateway"/> (ADR-0006). Extraídos
/// da classe pra que os testes de resiliência usem janelas curtas (circuito abre e se
/// recupera em milissegundos) sem precisar esperar os valores de produção.
/// </summary>
public sealed record LegadoResiliencePipelineOptions
{
    public int MaxRetryAttempts { get; init; } = 3;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan TimeoutPorTentativa { get; init; } = TimeSpan.FromSeconds(2);

    public double FailureRatio { get; init; } = 0.5;

    public int MinimumThroughput { get; init; } = 4;

    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    public static LegadoResiliencePipelineOptions Padrao { get; } = new();
}
