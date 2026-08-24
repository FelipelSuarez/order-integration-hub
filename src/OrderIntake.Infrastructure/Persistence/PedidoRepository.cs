using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Infrastructure.Persistence;

/// <summary>
/// A transação de criação é um INSERT puro — a chamada ao legado já aconteceu antes,
/// fora da transação (ADR-0002), sem invariante read-then-write a proteger aqui. Por
/// isso READ COMMITTED explícito basta; a concorrência real (transições de Status) é
/// protegida por rowversion otimista, não por isolamento mais forte (ADR-0004).
/// </summary>
public sealed class PedidoRepository(OrderIntakeDbContext context) : IPedidoRepository
{
    public async Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await context.Pedidos.AddAsync(pedido, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task SalvarAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
