namespace LegadoErp.Fake.Legado;

/// <summary>
/// Simula a decisão do ERP legado: aprova, recusa por regra de negócio (dados no
/// próprio DataContract, não um SOAP Fault) ou, quando <see cref="EstadoLegado.Indisponivel"/>
/// está ligado, lança uma exceção não tratada — o SoapCore converte isso num SOAP Fault,
/// reproduzindo o "legado fora do ar" que o cliente resiliente (Polly) precisa absorver.
/// </summary>
public sealed class ServicoLegado(EstadoLegado estado) : IServicoLegado
{
    private const int EstoqueMaximoPorItem = 100;

    public Task<ValidarEReservarPedidoResponse> ValidarEReservarPedido(ValidarEReservarPedidoRequest request)
    {
        estado.RegistrarChamada();

        if (estado.Indisponivel)
        {
            throw new InvalidOperationException("Legado indisponível (modo de simulação ligado).");
        }

        if (request.ClienteId == Guid.Empty)
        {
            return Task.FromResult(new ValidarEReservarPedidoResponse { Aprovado = false, Motivo = "Cliente inválido." });
        }

        var itemSemEstoque = request.Itens.FirstOrDefault(item => item.Quantidade > EstoqueMaximoPorItem);
        if (itemSemEstoque is not null)
        {
            return Task.FromResult(new ValidarEReservarPedidoResponse
            {
                Aprovado = false,
                Motivo = $"Estoque insuficiente para o produto {itemSemEstoque.ProdutoId}.",
            });
        }

        return Task.FromResult(new ValidarEReservarPedidoResponse { Aprovado = true });
    }
}
