using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Pedidos;
using OrderIntake.Infrastructure.Legado;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.Infrastructure.Sagas;

namespace OrderIntake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderIntakeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderIntakeDb")));

        services.AddScoped<IPedidoRepository, PedidoRepository>();

        // Leitura adiada pro momento da resolução (não no corpo de AddInfrastructure):
        // builder.Services.AddInfrastructure(builder.Configuration) roda antes de o
        // WebApplicationFactory de teste injetar seu override de configuração — ler
        // "Legado:EnderecoServico" aqui, cedo, capturaria sempre o valor de
        // appsettings.json, nunca o do teste. AddDbContext(options => ...) e
        // AddMassTransit(x => x.UsingRabbitMq((context, cfg) => ...)) já adiam a leitura
        // da mesma forma — não é um padrão novo neste arquivo.
        services.AddSingleton<ILegadoPedidoGateway>(_ =>
        {
            var enderecoServicoLegado = configuration["Legado:EnderecoServico"];
            if (string.IsNullOrWhiteSpace(enderecoServicoLegado))
            {
                throw new InvalidOperationException("Legado:EnderecoServico precisa estar configurado.");
            }

            return new LegadoPedidoGateway(enderecoServicoLegado);
        });
        services.AddSingleton(PedidoSagaOptions.Padrao);

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<OrderIntakeDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            // ADR-0011: substitui o PedidoRecebidoConsumer (ZER-161) — a saga assume o
            // ciclo de vida inteiro, incluindo Recebido → Validando, em vez de dividir a
            // orquestração entre um consumer simples e algo mais adiante.
            x.AddSagaStateMachine<PedidoValidacaoStateMachine, PedidoSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<OrderIntakeDbContext>();
                    r.UseSqlServer();
                });

            x.AddDelayedMessageScheduler();

            // Dedupe por MessageId (InboxState — existe no schema desde a ZER-161, inerte
            // até aqui). A guarda de Status usada pelo antigo PedidoRecebidoConsumer
            // (ADR-0007) não é suficiente pra saga: reentrega de um evento que a saga
            // processaria de novo no MESMO estado (ex.: ReavaliarPedido enquanto ainda em
            // Validando) chamaria o legado — efeito colateral não-idempotente — outra vez.
            x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<OrderIntakeDbContext>(context));

            // Só definido em teste (OrderIntakeApiFactory): cada host de teste cria seu
            // próprio bus contra o mesmo broker compartilhado (RabbitMqContainerFixture).
            // Sem um prefixo próprio, todos bindariam a mesma fila (nome deriva do tipo do
            // consumer) e virariam consumers concorrentes — mensagem de um teste podia ser
            // entregue ao host (já sendo descartado) de outro teste.
            var endpointNamePrefix = configuration["Messaging:EndpointNamePrefix"];
            if (!string.IsNullOrWhiteSpace(endpointNamePrefix))
            {
                x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(endpointNamePrefix, includeNamespace: false));
            }

            if (string.Equals(configuration["Messaging:Transport"], "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
            {
                var azureServiceBusConnectionString = configuration.GetConnectionString("AzureServiceBus");
                if (string.IsNullOrWhiteSpace(azureServiceBusConnectionString))
                {
                    throw new InvalidOperationException(
                        "Messaging:Transport=AzureServiceBus exige ConnectionStrings:AzureServiceBus configurada.");
                }

                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(azureServiceBusConnectionString);
                    // ASB agenda nativamente (ScheduledEnqueueTimeUtc) — UseDelayedMessageScheduler
                    // é o mecanismo do RabbitMQ (plugin de delayed exchange), não funciona aqui.
                    cfg.UseServiceBusMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var connectionString = configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672/";
                    cfg.Host(new Uri(connectionString));
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
