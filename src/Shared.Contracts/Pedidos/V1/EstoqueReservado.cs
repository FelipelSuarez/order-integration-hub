namespace Shared.Contracts.Pedidos.V1;

public sealed record EstoqueReservado(Guid PedidoId, Guid ClienteId, DateTimeOffset OcorridoEm);
