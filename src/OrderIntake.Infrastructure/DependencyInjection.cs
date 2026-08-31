using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Pedidos;
using OrderIntake.Infrastructure.Legado;
using OrderIntake.Infrastructure.Messaging;
using OrderIntake.Infrastructure.Persistence;

namespace OrderIntake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderIntakeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderIntakeDb")));

        services.AddScoped<IPedidoRepository, PedidoRepository>();

        services.AddSingleton<ILegadoPedidoGateway>(_ =>
            new LegadoPedidoGateway(configuration["Legado:EnderecoServico"] ?? "http://localhost:5236/ServicoLegado.svc"));

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<OrderIntakeDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            x.AddConsumer<PedidoRecebidoConsumer>();

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
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var connectionString = configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672/";
                    cfg.Host(new Uri(connectionString));
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
