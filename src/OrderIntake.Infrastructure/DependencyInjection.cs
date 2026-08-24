using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Infrastructure.Persistence;

namespace OrderIntake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderIntakeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderIntakeDb")));

        return services;
    }
}
