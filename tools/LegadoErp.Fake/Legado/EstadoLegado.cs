namespace LegadoErp.Fake.Legado;

/// <summary>
/// Chave liga/desliga em memória, controlada pelo endpoint /admin/indisponibilidade, para
/// que os testes simulem o legado fora do ar sem precisar derrubar o processo.
/// </summary>
public sealed class EstadoLegado
{
    private volatile bool _indisponivel;
    private int _chamadas;

    public bool Indisponivel
    {
        get => _indisponivel;
        set => _indisponivel = value;
    }

    /// <summary>Quantas vezes ValidarEReservarPedido foi invocado — prova de dedupe em teste.</summary>
    public int Chamadas => _chamadas;

    public void RegistrarChamada() => Interlocked.Increment(ref _chamadas);
}
