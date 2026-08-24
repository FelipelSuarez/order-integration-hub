using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;

namespace OrderIntake.Infrastructure.Persistence;

/// <summary>
/// Isolamento e concorrência decididos na ADR-0004: a transação de criação é um INSERT
/// puro (a chamada ao legado, cujo resultado decide o que é gravado, já aconteceu antes,
/// fora dela — fronteira definida na ADR-0002), então READ COMMITTED explícito basta. A
/// concorrência real — transições de Status do mesmo Pedido — é protegida por
/// rowversion otimista, não por isolamento mais forte.
/// </summary>
public sealed class PedidoRepository(OrderIntakeDbContext context) : IPedidoRepository
{
    public async Task AdicionarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        context.Pedidos.Add(pedido);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task SalvarAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
