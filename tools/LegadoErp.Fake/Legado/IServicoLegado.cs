using System.Runtime.Serialization;
using System.ServiceModel;

namespace LegadoErp.Fake.Legado;

[ServiceContract(Namespace = "http://legado.zeraustech.com/pedidos")]
public interface IServicoLegado
{
    [OperationContract]
    Task<ValidarEReservarPedidoResponse> ValidarEReservarPedido(ValidarEReservarPedidoRequest request);
}

[DataContract(Namespace = "http://legado.zeraustech.com/pedidos")]
public sealed class ValidarEReservarPedidoRequest
{
    [DataMember]
    public Guid ClienteId { get; set; }

    [DataMember]
    public List<ItemPedidoLegado> Itens { get; set; } = [];
}

[DataContract(Namespace = "http://legado.zeraustech.com/pedidos")]
public sealed class ItemPedidoLegado
{
    [DataMember]
    public Guid ProdutoId { get; set; }

    [DataMember]
    public int Quantidade { get; set; }
}

[DataContract(Namespace = "http://legado.zeraustech.com/pedidos")]
public sealed class ValidarEReservarPedidoResponse
{
    [DataMember]
    public bool Aprovado { get; set; }

    [DataMember]
    public string? Motivo { get; set; }
}
