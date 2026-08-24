using Microsoft.EntityFrameworkCore;

namespace OrderIntake.Infrastructure.Persistence;

public sealed class OrderIntakeDbContext(DbContextOptions<OrderIntakeDbContext> options) : DbContext(options);
