namespace OrderIntake.Domain.Pedidos;

public sealed class Item
{
    private Item()
    {
    }

    private Item(Guid produtoId, int quantidade)
    {
        Id = Guid.NewGuid();
        ProdutoId = produtoId;
        Quantidade = quantidade;
    }

    public Guid Id { get; private set; }

    public Guid ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    internal static Item Criar(Guid produtoId, int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new InvalidOperationException("Quantidade precisa ser maior que zero.");
        }

        return new Item(produtoId, quantidade);
    }
}
