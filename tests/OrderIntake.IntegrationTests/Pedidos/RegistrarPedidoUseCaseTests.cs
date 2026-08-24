using Microsoft.EntityFrameworkCore;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;

namespace OrderIntake.IntegrationTests.Pedidos;

[Collection(nameof(SqlServerCollection))]
public sealed class RegistrarPedidoUseCaseTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task ExecutarAsync_PersisteOPedidoEOsItens()
    {
        var options = new DbContextOptionsBuilder<OrderIntakeDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        var clienteId = Guid.NewGuid();
        var itens = new List<(Guid ProdutoId, int Quantidade)> { (Guid.NewGuid(), 3), (Guid.NewGuid(), 1) };

        Pedido pedido;

        await using (var context = new OrderIntakeDbContext(options))
        {
            var useCase = new RegistrarPedidoUseCase(new PedidoRepository(context));
            pedido = await useCase.ExecutarAsync(new RegistrarPedidoCommand(clienteId, itens), CancellationToken.None);
        }

        await using var readContext = new OrderIntakeDbContext(options);
        var persistido = await readContext.Pedidos.Include(p => p.Itens).FirstAsync(p => p.Id == pedido.Id);

        persistido.ClienteId.Should().Be(clienteId);
        persistido.Status.Should().Be(Status.Recebido);
        persistido.Itens.Should().HaveCount(2);
    }
}
