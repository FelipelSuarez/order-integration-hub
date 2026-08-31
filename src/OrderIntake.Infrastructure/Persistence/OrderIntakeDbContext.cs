using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Sagas;

namespace OrderIntake.Infrastructure.Persistence;

public sealed class OrderIntakeDbContext(DbContextOptions<OrderIntakeDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<PedidoSagaState> PedidoSagaState => Set<PedidoSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderIntakeDbContext).Assembly);

        // InboxState também entra: OutboxMessage tem FK opcional pra ela mesmo sem uso de
        // dedupe por inbox (ADR-0007) — a tabela existe, mas nada escreve nela.
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddInboxStateEntity();
    }
}
