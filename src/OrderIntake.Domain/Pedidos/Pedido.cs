namespace OrderIntake.Domain.Pedidos;

public sealed class Pedido
{
    private readonly List<Item> _itens = [];

    private Pedido()
    {
    }

    private Pedido(Guid clienteId)
    {
        Id = Guid.NewGuid();
        ClienteId = clienteId;
        Status = Status.Recebido;
    }

    public Guid Id { get; private set; }

    public Guid ClienteId { get; private set; }

    public Status Status { get; private set; }

    public string? MotivoRejeicao { get; private set; }

    public IReadOnlyCollection<Item> Itens => _itens.AsReadOnly();

    public static Pedido Registrar(Guid clienteId, IReadOnlyCollection<(Guid ProdutoId, int Quantidade)> itens)
    {
        if (itens.Count == 0)
        {
            throw new InvalidOperationException("Pedido precisa de ao menos um item.");
        }

        var pedido = new Pedido(clienteId);

        foreach (var (produtoId, quantidade) in itens)
        {
            pedido._itens.Add(Item.Criar(produtoId, quantidade));
        }

        return pedido;
    }

    public void IniciarValidacao()
    {
        if (Status != Status.Recebido)
        {
            throw new InvalidOperationException($"Não é possível iniciar validação a partir de {Status}.");
        }

        Status = Status.Validando;
    }

    public void ConfirmarReserva()
    {
        if (Status != Status.Validando)
        {
            throw new InvalidOperationException($"Não é possível confirmar reserva a partir de {Status}.");
        }

        Status = Status.Reservado;
    }

    public void Rejeitar(string motivo)
    {
        if (Status != Status.Validando)
        {
            throw new InvalidOperationException($"Não é possível rejeitar a partir de {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        Status = Status.Rejeitado;
        MotivoRejeicao = motivo;
    }
}
