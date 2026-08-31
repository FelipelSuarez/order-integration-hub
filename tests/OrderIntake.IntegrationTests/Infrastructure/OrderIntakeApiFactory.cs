using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Infrastructure.Sagas;

namespace OrderIntake.IntegrationTests.Infrastructure;

public sealed class OrderIntakeApiFactory(
    string connectionString,
    string rabbitMqConnectionString,
    string? legadoEnderecoServico = null,
    PedidoSagaOptions? pedidoSagaOptions = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var configuracao = new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderIntakeDb"] = connectionString,
                ["ConnectionStrings:RabbitMq"] = rabbitMqConnectionString,
                // Isola a fila deste host das de outros testes no mesmo RabbitMqContainerFixture
                // compartilhado — sem isso, hosts concorrentes disputam a mesma fila (nome
                // deriva do tipo do consumer) e uma mensagem pode ir parar num host já descartado.
                ["Messaging:EndpointNamePrefix"] = $"test-{Guid.NewGuid():N}",
            };

            if (legadoEnderecoServico is not null)
            {
                configuracao["Legado:EnderecoServico"] = legadoEnderecoServico;
            }

            configBuilder.AddInMemoryCollection(configuracao);
        });

        if (pedidoSagaOptions is not null)
        {
            // Registrado depois do AddInfrastructure: a última inscrição de um singleton
            // vence na resolução do DI, sem precisar de Replace/RemoveAll.
            builder.ConfigureServices(services => services.AddSingleton(pedidoSagaOptions));
        }
    }
}
