namespace OrderIntake.Application.Pedidos;

/// <summary>
/// O legado não respondeu de forma confiável (circuito aberto, timeout ou falha técnica
/// persistente depois das tentativas de resiliência) — não é uma rejeição de negócio.
/// Quem consome <see cref="ILegadoPedidoGateway"/> não deve tratar isto como
/// <see cref="ResultadoLegado.Recusado"/>: o Pedido precisa continuar elegível para
/// reprocessamento, não ser rejeitado (ADR-0006).
/// </summary>
public sealed class LegadoIndisponivelException(string message, Exception innerException)
    : Exception(message, innerException);
