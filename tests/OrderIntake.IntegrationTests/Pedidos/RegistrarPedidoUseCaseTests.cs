using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        // Este teste cobre persistência, não mensageria (isso é o
        // PedidoRecebidoMessagingTests) — um bus em memória basta pra satisfazer
        // IPublishEndpoint sem depender de RabbitMQ de verdade.
        await using var busProvider = new ServiceCollection()
            .AddMassTransit(x => x.UsingInMemory())
            .BuildServiceProvider();
        var busControl = busProvider.GetRequiredService<IBusControl>();
        await busControl.StartAsync();

        Pedido pedido;

        try
        {
            await using (var context = new OrderIntakeDbContext(options))
            {
                var useCase = new RegistrarPedidoUseCase(new PedidoRepository(context, busControl));
                pedido = await useCase.ExecutarAsync(new RegistrarPedidoCommand(clienteId, itens), CancellationToken.None);
            }
        }
        finally
        {
            await busControl.StopAsync();
        }

        await using var readContext = new OrderIntakeDbContext(options);
        var persistido = await readContext.Pedidos.Include(p => p.Itens).FirstAsync(p => p.Id == pedido.Id);

        persistido.ClienteId.Should().Be(clienteId);
        persistido.Status.Should().Be(Status.Recebido);
        persistido.Itens.Should().HaveCount(2);
    }
}
