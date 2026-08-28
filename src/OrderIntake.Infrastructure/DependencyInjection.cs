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
