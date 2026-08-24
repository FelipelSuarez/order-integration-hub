namespace OrderIntake.Api.Pedidos;

public sealed record RegistrarPedidoRequest(Guid ClienteId, IReadOnlyCollection<ItemRequest> Itens);
