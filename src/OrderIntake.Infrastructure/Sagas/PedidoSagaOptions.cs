namespace OrderIntake.Infrastructure.Sagas;

/// <summary>
/// Parâmetros de retentativa/timeout da <see cref="PedidoValidacaoStateMachine"/>
/// (ADR-0011). Extraídos pra que os testes usem intervalos curtos (retentativa e
/// orçamento em milissegundos) sem depender dos valores de produção — mesmo padrão de
/// <c>LegadoResiliencePipelineOptions</c> na ZER-162.
/// </summary>
public sealed record PedidoSagaOptions
{
    /// <summary>Espera entre uma tentativa de validação falha (legado indisponível) e a próxima.</summary>
    public TimeSpan IntervaloRetentativa { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Orçamento total, a partir da primeira tentativa, antes de desistir e rejeitar por
    /// timeout técnico.
    /// </summary>
    public TimeSpan OrcamentoTotal { get; init; } = TimeSpan.FromMinutes(15);

    public static PedidoSagaOptions Padrao { get; } = new();
}
