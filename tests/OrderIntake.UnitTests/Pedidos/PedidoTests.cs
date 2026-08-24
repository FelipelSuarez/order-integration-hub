using OrderIntake.Domain.Pedidos;

namespace OrderIntake.UnitTests.Pedidos;

public sealed class PedidoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    [Fact]
    public void Registrar_SemItens_Falha()
    {
        var registrar = () => Pedido.Registrar(ClienteId, []);

        registrar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Registrar_ComQuantidadeInvalida_Falha()
    {
        var registrar = () => Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 0)]);

        registrar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Registrar_ComItensValidos_ComecaComoRecebido()
    {
        var pedido = Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 2)]);

        pedido.Status.Should().Be(Status.Recebido);
        pedido.Itens.Should().HaveCount(1);
    }

    [Fact]
    public void SequenciaDeTransicoesValida_ChegaEmReservado()
    {
        var pedido = Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 1)]);

        pedido.IniciarValidacao();
        pedido.ConfirmarReserva();

        pedido.Status.Should().Be(Status.Reservado);
    }

    [Fact]
    public void Rejeitar_AposValidando_RegistraMotivo()
    {
        var pedido = Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 1)]);

        pedido.IniciarValidacao();
        pedido.Rejeitar("estoque insuficiente");

        pedido.Status.Should().Be(Status.Rejeitado);
        pedido.MotivoRejeicao.Should().Be("estoque insuficiente");
    }

    [Fact]
    public void ConfirmarReserva_SemPassarPorValidando_Falha()
    {
        var pedido = Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 1)]);

        var confirmar = () => pedido.ConfirmarReserva();

        confirmar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IniciarValidacao_APartirDeEstadoTerminal_Falha()
    {
        var pedido = Pedido.Registrar(ClienteId, [(Guid.NewGuid(), 1)]);
        pedido.IniciarValidacao();
        pedido.ConfirmarReserva();

        var iniciarDeNovo = () => pedido.IniciarValidacao();

        iniciarDeNovo.Should().Throw<InvalidOperationException>();
    }
}
