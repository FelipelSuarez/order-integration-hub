namespace OrderIntake.Api.Pedidos;

public sealed record ItemRequest(Guid ProdutoId, int Quantidade);
