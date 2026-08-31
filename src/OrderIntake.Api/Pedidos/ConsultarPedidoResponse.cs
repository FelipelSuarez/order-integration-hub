namespace OrderIntake.Api.Pedidos;

public sealed record ConsultarPedidoResponse(Guid PedidoId, string Status, string? MotivoRejeicao);
