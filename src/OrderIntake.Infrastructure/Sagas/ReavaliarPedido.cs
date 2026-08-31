namespace OrderIntake.Infrastructure.Sagas;

/// <summary>
/// Mensagem interna de retentativa da saga — não é um evento de domínio (não vai pro
/// Shared.Contracts), só agenda a próxima tentativa de validação quando o legado está
/// indisponível.
/// </summary>
public sealed record ReavaliarPedido(Guid PedidoId);
