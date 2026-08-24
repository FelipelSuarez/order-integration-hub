using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace OrderIntake.IntegrationTests.Infrastructure;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04").Build();

    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = "OrderIntakeDb",
            };

            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<OrderIntakeDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var context = new OrderIntakeDbContext(options);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
