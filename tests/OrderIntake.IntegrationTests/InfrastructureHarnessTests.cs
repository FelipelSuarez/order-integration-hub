using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.IntegrationTests.Infrastructure;

namespace OrderIntake.IntegrationTests;

[Collection(nameof(IntegrationCollection))]
public sealed class InfrastructureHarnessTests(SqlServerContainerFixture fixture, RabbitMqContainerFixture rabbitMqFixture)
{
    [Fact]
    public async Task ContainerSobeMigrationAplicaEConexaoAbre()
    {
        await using var factory = new OrderIntakeApiFactory(fixture.ConnectionString, rabbitMqFixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderIntakeDbContext>();

        var conexaoAbriu = await dbContext.Database.CanConnectAsync();

        conexaoAbriu.Should().BeTrue();
    }
}
