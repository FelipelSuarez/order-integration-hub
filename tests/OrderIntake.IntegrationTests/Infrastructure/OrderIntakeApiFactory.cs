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
            });
        });
    }
}
