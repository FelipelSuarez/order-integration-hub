using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderIntake.Infrastructure.Persistence;

/// <summary>
/// Usada apenas em design-time pelo `dotnet ef migrations add`, que precisa instanciar o
/// DbContext sem subir o host da API. A connection string abaixo nunca é usada em runtime.
/// </summary>
public sealed class OrderIntakeDbContextFactory : IDesignTimeDbContextFactory<OrderIntakeDbContext>
{
    public OrderIntakeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderIntakeDbContext>()
            .UseSqlServer("Server=localhost;Database=OrderIntake;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new OrderIntakeDbContext(options);
    }
}
