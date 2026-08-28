namespace OrderIntake.Application.Pedidos;

/// <summary>
/// Porta para a decisão do ERP legado sobre um Pedido (validação de cliente/itens e
/// reserva de estoque, na mesma chamada — ver docs/domain.md). A implementação em
/// Infrastructure fala SOAP; nada disso atravessa esta interface (ADR-0006).
/// </summary>
public interface ILegadoPedidoGateway
{
    /// <exception cref="LegadoIndisponivelException">
    /// O legado não pôde ser consultado (circuito aberto ou falha técnica persistente) —
    /// não é uma decisão de negócio, então não deve ser tratado como
    /// <see cref="ResultadoLegado.Recusado"/>.
    /// </exception>
    Task<ResultadoLegado> ValidarEReservarAsync(
        Guid clienteId,
        IReadOnlyCollection<(Guid ProdutoId, int Quantidade)> itens,
        CancellationToken cancellationToken);
}
