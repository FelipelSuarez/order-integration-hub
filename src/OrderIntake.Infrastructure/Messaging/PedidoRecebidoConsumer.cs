using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;
using Shared.Contracts.Pedidos.V1;

namespace OrderIntake.Infrastructure.Messaging;

/// <summary>
/// Idempotente por guarda de estado (ADR-0007): só aplica a transição se o Pedido ainda
/// estiver em Recebido. Reentrega do mesmo PedidoRecebido é um no-op, não um erro. Sob
/// duas entregas concorrentes (at-least-once), ambas podem ler Status == Recebido antes
/// de qualquer uma salvar — a perdedora da corrida de rowversion (ADR-0004) trata o
/// conflito como o mesmo no-op, não como falha a ser reprocessada.
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

        try
        {
            await repository.SalvarAsync(context.CancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra entrega concorrente do mesmo PedidoRecebido já aplicou a transição.
        }
    }
}
