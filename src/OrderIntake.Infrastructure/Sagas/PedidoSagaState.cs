using MassTransit;

namespace OrderIntake.Infrastructure.Sagas;

/// <summary>
/// Bookkeeping do MassTransit para <see cref="PedidoValidacaoStateMachine"/> — não é o
/// agregado de domínio. <see cref="OrderIntake.Domain.Pedidos.Pedido"/> não pode
/// implementar <see cref="SagaStateMachineInstance"/> (tipo de infraestrutura); por isso
/// os dois guardam status em paralelo, com propósitos diferentes: <see cref="CurrentState"/>
/// decide qual handler da state machine dispara, <c>Pedido.Status</c> é o que
/// <c>GET /pedidos/{id}</c> lê (ADR-0011).
/// </summary>
public sealed class PedidoSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    /// <summary>Quando a primeira tentativa de validação aconteceu — base do orçamento de retentativa.</summary>
    public DateTimeOffset? PrimeiraTentativaEm { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
