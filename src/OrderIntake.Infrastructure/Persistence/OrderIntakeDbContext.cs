using Microsoft.EntityFrameworkCore;
using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Infrastructure.Persistence;

public sealed class OrderIntakeDbContext(DbContextOptions<OrderIntakeDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderIntakeDbContext).Assembly);
    }
}
