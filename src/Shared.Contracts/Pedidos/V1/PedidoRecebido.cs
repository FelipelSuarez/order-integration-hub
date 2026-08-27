namespace Shared.Contracts.Pedidos.V1;

public sealed record PedidoRecebido(Guid PedidoId, Guid ClienteId, DateTimeOffset OcorridoEm);
