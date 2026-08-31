namespace Shared.Contracts.Pedidos.V1;

public sealed record PedidoValidado(Guid PedidoId, Guid ClienteId, DateTimeOffset OcorridoEm);
