using OrderIntake.Api.Pedidos;
using OrderIntake.Application.Pedidos;
using OrderIntake.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RegistrarPedidoUseCase>();

var app = builder.Build();

app.MapPedidoEndpoints();

app.Run();

public partial class Program;
