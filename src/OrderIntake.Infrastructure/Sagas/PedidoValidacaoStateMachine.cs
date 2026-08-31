using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Pedidos;
using OrderIntake.Domain.Pedidos;
using Shared.Contracts.Pedidos.V1;

namespace OrderIntake.Infrastructure.Sagas;

/// <summary>
/// Orquestra a validação assíncrona do Pedido contra o legado (ADR-0011): a partir de
/// Validando, chama <see cref="ILegadoPedidoGateway"/>; aprovado vira Reservado, recusa de
/// negócio vira Rejeitado, e legado indisponível (<see cref="LegadoIndisponivelException"/>)
/// não rejeita — permanece em Validando e reagenda uma nova tentativa, dentro do orçamento
/// de <see cref="PedidoSagaOptions.OrcamentoTotal"/>. Substitui o PedidoRecebidoConsumer da
/// ZER-161, que só fazia metade do trabalho (Recebido → Validando, sem chamar o legado).
/// </summary>
public sealed class PedidoValidacaoStateMachine : MassTransitStateMachine<PedidoSagaState>
{
    private readonly ILegadoPedidoGateway _legadoPedidoGateway;
    private readonly PedidoSagaOptions _opcoes;

    public State Validando { get; private set; } = null!;

    public State Reservado { get; private set; } = null!;

    public State Rejeitado { get; private set; } = null!;

    public Event<PedidoRecebido> PedidoRecebidoEvent { get; private set; } = null!;

    public Event<ReavaliarPedido> ReavaliarPedidoEvent { get; private set; } = null!;

    public PedidoValidacaoStateMachine(ILegadoPedidoGateway legadoPedidoGateway, PedidoSagaOptions opcoes)
    {
        _legadoPedidoGateway = legadoPedidoGateway;
        _opcoes = opcoes;

        InstanceState(x => x.CurrentState);

        Event(() => PedidoRecebidoEvent, x => x.CorrelateById(context => context.Message.PedidoId));
        Event(() => ReavaliarPedidoEvent, x => x.CorrelateById(context => context.Message.PedidoId));

        Initially(
            When(PedidoRecebidoEvent)
                .Then(context => context.Saga.PrimeiraTentativaEm = DateTimeOffset.UtcNow)
                .ThenAsync(IniciarValidacaoAsync)
                .ThenAsync(ProcessarValidacaoAsync));

        During(Validando,
            When(ReavaliarPedidoEvent)
                .ThenAsync(ProcessarValidacaoAsync));

        // Duas situações de reentrega legítima (ADR-0007), nenhuma delas erro:
        // - uma reavaliação em atraso chega depois de o Pedido já ter saído de Validando
        //   (aprovado/recusado enquanto ela estava agendada);
        // - um PedidoRecebido chega de novo pra uma instância que já saiu do estado
        //   inicial (Initially só corresponde à criação da instância).
        // Sem isso, MassTransit trata como evento sem handler no estado atual e falha.
        DuringAny(
            When(ReavaliarPedidoEvent).Then(_ => { }),
            When(PedidoRecebidoEvent).Then(_ => { }));
    }

    private async Task IniciarValidacaoAsync(BehaviorContext<PedidoSagaState, PedidoRecebido> context)
    {
        var servicos = context.GetPayload<IServiceProvider>();
        var repository = servicos.GetRequiredService<IPedidoRepository>();

        var pedido = await repository.ObterPorIdAsync(context.Message.PedidoId, context.CancellationToken);
        if (pedido is null || pedido.Status != Status.Recebido)
        {
            return;
        }

        pedido.IniciarValidacao();
        await repository.SalvarAsync(context.CancellationToken);
        context.Saga.CurrentState = Validando.Name;
    }

    private async Task ProcessarValidacaoAsync(BehaviorContext<PedidoSagaState> context)
    {
        var servicos = context.GetPayload<IServiceProvider>();
        var repository = servicos.GetRequiredService<IPedidoRepository>();

        var pedido = await repository.ObterPorIdAsync(context.Saga.CorrelationId, context.CancellationToken);
        if (pedido is null || pedido.Status != Status.Validando)
        {
            return;
        }

        var itens = pedido.Itens.Select(item => (item.ProdutoId, item.Quantidade)).ToList();

        try
        {
            var resultado = await _legadoPedidoGateway.ValidarEReservarAsync(pedido.ClienteId, itens, context.CancellationToken);

            switch (resultado)
            {
                case ResultadoLegado.Aprovado:
                    pedido.ConfirmarReserva();
                    await repository.SalvarAsync(context.CancellationToken);
                    await context.Publish(new PedidoValidado(pedido.Id, pedido.ClienteId, DateTimeOffset.UtcNow));
                    await context.Publish(new EstoqueReservado(pedido.Id, pedido.ClienteId, DateTimeOffset.UtcNow));
                    context.Saga.CurrentState = Reservado.Name;
                    break;

                case ResultadoLegado.Recusado recusado:
                    await RejeitarAsync(context, repository, pedido, recusado.Motivo);
                    break;
            }
        }
        catch (LegadoIndisponivelException)
        {
            await ReagendarOuDesistirAsync(context, repository, pedido);
        }
    }

    private async Task ReagendarOuDesistirAsync(BehaviorContext<PedidoSagaState> context, IPedidoRepository repository, Pedido pedido)
    {
        context.Saga.PrimeiraTentativaEm ??= DateTimeOffset.UtcNow;
        var decorrido = DateTimeOffset.UtcNow - context.Saga.PrimeiraTentativaEm.Value;

        if (decorrido >= _opcoes.OrcamentoTotal)
        {
            await RejeitarAsync(context, repository, pedido, "Legado indisponível: orçamento de retentativa esgotado.");
            return;
        }

        await context.ScheduleSend(_opcoes.IntervaloRetentativa, new ReavaliarPedido(pedido.Id));
    }

    private async Task RejeitarAsync(BehaviorContext<PedidoSagaState> context, IPedidoRepository repository, Pedido pedido, string motivo)
    {
        pedido.Rejeitar(motivo);
        await repository.SalvarAsync(context.CancellationToken);
        await context.Publish(new PedidoRejeitado(pedido.Id, pedido.ClienteId, motivo, DateTimeOffset.UtcNow));
        context.Saga.CurrentState = Rejeitado.Name;
    }
}
