using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;

namespace OrderIntake.IntegrationTests.Infrastructure;

/// <summary>
/// Espera um Pedido chegar a um Status via polling no banco — a única forma confiável de
/// saber que o outbox entregou e o consumer processou antes de descartar o
/// <see cref="OrderIntakeApiFactory"/>. Descartar o host antes disso é o bug real por trás
/// de "ObjectDisposedException: IServiceProvider" em MassTransit.ReceiveTransport: a
/// entrega do outbox é assíncrona, e um teste que publica sem esperar pode derrubar o
/// host no meio do processamento da própria mensagem que acabou de gerar.
///
/// Flakiness residual conhecida (ZER-183): rodando a suíte inteira, ou várias classes de
/// IntegrationCollection juntas, PostPedidos_ComPayloadValido_Retorna202EPersisteOPedido
/// ocasionalmente ainda estoura esse timeout — SQL Server + RabbitMQ + múltiplos hosts
/// Kestrel in-process (LegadoFakeHostFixture) competindo por CPU/memória na mesma
/// máquina, às vezes lento o bastante pra atrasar até a transição Recebido→Validando.
/// Passa de forma confiável isolado ou em combinações menores. Mesma categoria de
/// contenção já documentada no commit f60bab9 (ZER-162) — não é falha de lógica.
/// </summary>
internal static class PedidoAguardo
{
    public static async Task<Pedido> AguardarStatusAsync(OrderIntakeApiFactory factory, Guid pedidoId, Status statusEsperado)
    {
        // 180s: folga generosa pra primeira execução numa máquina limpa (imagens ainda não
        // em cache) e pro caso de rodar junto com SqlServerCollection — outro
        // SqlServerContainerFixture, cujo container fica vivo até o assembly inteiro
        // terminar (ICollectionFixture não é escopado por collection), concorrendo por
        // CPU/memória com o SQL Server + RabbitMQ desta collection e com os hosts Kestrel
        // in-process do LegadoFakeHostFixture (ZER-162/183) usados por vários testes.
        var limite = DateTime.UtcNow.AddSeconds(180);

        while (DateTime.UtcNow < limite)
        {
            var pedido = await ObterPedidoAsync(factory, pedidoId);

            if (pedido?.Status == statusEsperado)
            {
                return pedido;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Pedido {pedidoId} não chegou a {statusEsperado} a tempo.");
    }

    public static async Task<Pedido?> ObterPedidoAsync(OrderIntakeApiFactory factory, Guid pedidoId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderIntakeDbContext>();

        return await context.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pedidoId);
    }
}
