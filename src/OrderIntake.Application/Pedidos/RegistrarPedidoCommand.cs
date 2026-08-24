namespace OrderIntake.Application.Pedidos;

public sealed record RegistrarPedidoCommand(Guid ClienteId, IReadOnlyCollection<(Guid ProdutoId, int Quantidade)> Itens);
