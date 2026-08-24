using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Application.Pedidos;

public sealed class RegistrarPedidoUseCase(IPedidoRepository repository)
{
    public async Task<Pedido> ExecutarAsync(RegistrarPedidoCommand command, CancellationToken cancellationToken)
    {
        var pedido = Pedido.Registrar(command.ClienteId, command.Itens);

        await repository.AdicionarAsync(pedido, cancellationToken);

        return pedido;
    }
}
