using LegadoErp.Fake.Legado;
using SoapCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSoapCore();
builder.Services.AddSingleton<EstadoLegado>();
builder.Services.AddSingleton<IServicoLegado, ServicoLegado>();

var app = builder.Build();

((IApplicationBuilder)app).UseSoapEndpoint<IServicoLegado>(
    "/ServicoLegado.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);

app.MapPost("/admin/indisponibilidade", (bool ativo, EstadoLegado estado) =>
{
    estado.Indisponivel = ativo;
    return Results.NoContent();
});

app.Run();

public partial class Program;
