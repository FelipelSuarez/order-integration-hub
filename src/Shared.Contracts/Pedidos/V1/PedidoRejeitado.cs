namespace Shared.Contracts.Pedidos.V1;

public sealed record PedidoRejeitado(Guid PedidoId, Guid ClienteId, string Motivo, DateTimeOffset OcorridoEm);
