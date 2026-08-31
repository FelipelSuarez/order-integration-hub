namespace OrderIntake.Application.Pedidos;

/// <summary>
/// Decisão do legado sobre um Pedido — sempre uma resposta de negócio válida. Uma
/// falha técnica na chamada (legado fora do ar, circuito aberto) nunca vira um valor
/// deste tipo; vira <see cref="LegadoIndisponivelException"/>.
/// </summary>
public abstract record ResultadoLegado
{
    private ResultadoLegado()
    {
    }

    public sealed record Aprovado : ResultadoLegado;

    public sealed record Recusado(string Motivo) : ResultadoLegado;
}
