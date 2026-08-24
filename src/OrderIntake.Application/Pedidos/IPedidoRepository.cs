using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Application.Pedidos;

public interface IPedidoRepository
{
    Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken);

    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task SalvarAsync(CancellationToken cancellationToken);
}
