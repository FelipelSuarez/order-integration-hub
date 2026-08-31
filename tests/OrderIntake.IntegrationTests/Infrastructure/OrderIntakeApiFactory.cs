using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrderIntake.IntegrationTests.Infrastructure;

public sealed class OrderIntakeApiFactory(string connectionString, string rabbitMqConnectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderIntakeDb"] = connectionString,
                ["ConnectionStrings:RabbitMq"] = rabbitMqConnectionString,
                // Isola a fila deste host das de outros testes no mesmo RabbitMqContainerFixture
                // compartilhado — sem isso, hosts concorrentes disputam a mesma fila (nome
                // deriva do tipo do consumer) e uma mensagem pode ir parar num host já descartado.
                ["Messaging:EndpointNamePrefix"] = $"test-{Guid.NewGuid():N}",
            });
        });
    }
}
