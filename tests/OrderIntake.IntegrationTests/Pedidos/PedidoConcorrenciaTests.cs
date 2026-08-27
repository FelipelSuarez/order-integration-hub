using Microsoft.EntityFrameworkCore;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;

namespace OrderIntake.IntegrationTests.Pedidos;

[Collection(nameof(IntegrationCollection))]
public sealed class PedidoConcorrenciaTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task TransicaoConcorrenteDeStatusLancaConflito()
    {
        var options = new DbContextOptionsBuilder<OrderIntakeDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        var pedido = Pedido.Registrar(Guid.NewGuid(), [(Guid.NewGuid(), 2)]);

        await using (var setupContext = new OrderIntakeDbContext(options))
        {
            setupContext.Pedidos.Add(pedido);
            await setupContext.SaveChangesAsync();
        }

        await using var context1 = new OrderIntakeDbContext(options);
        await using var context2 = new OrderIntakeDbContext(options);

        var pedido1 = await context1.Pedidos.FirstAsync(p => p.Id == pedido.Id);
        var pedido2 = await context2.Pedidos.FirstAsync(p => p.Id == pedido.Id);

        pedido1.IniciarValidacao();
        await context1.SaveChangesAsync();

        pedido2.IniciarValidacao();
        var salvarComVersaoDesatualizada = async () => await context2.SaveChangesAsync();

        await salvarComVersaoDesatualizada.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
