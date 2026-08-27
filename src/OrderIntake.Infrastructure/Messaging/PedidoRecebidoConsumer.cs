using MassTransit;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;
using Shared.Contracts.Pedidos.V1;

namespace OrderIntake.Infrastructure.Messaging;

/// <summary>
/// Idempotente por guarda de estado (ADR-0007): só aplica a transição se o Pedido ainda
/// estiver em Recebido. Reentrega do mesmo PedidoRecebido é um no-op, não um erro.
/// </summary>
public sealed class PedidoRecebidoConsumer(IPedidoRepository repository) : IConsumer<PedidoRecebido>
{
    public async Task Consume(ConsumeContext<PedidoRecebido> context)
    {
        var pedido = await repository.ObterPorIdAsync(context.Message.PedidoId, context.CancellationToken);

        if (pedido is null || pedido.Status != Status.Recebido)
        {
            return;
        }

        pedido.IniciarValidacao();

        await repository.SalvarAsync(context.CancellationToken);
    }
}
