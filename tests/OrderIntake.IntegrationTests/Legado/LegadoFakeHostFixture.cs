extern alias LegadoFake;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoapCore;
using Fake = LegadoFake::LegadoErp.Fake.Legado;

namespace OrderIntake.IntegrationTests.Legado;

/// <summary>
/// Sobe o LegadoErp.Fake de verdade num Kestrel real (o cliente SOAP do LegadoPedidoGateway
/// usa BasicHttpBinding sobre HTTP de verdade — não dá pra usar o TestServer em memória de
/// um WebApplicationFactory comum). Alias de projeto porque LegadoErp.Fake e OrderIntake.Api
/// têm, cada um, sua própria classe Program no namespace global.
/// </summary>
public sealed class LegadoFakeHostFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private Fake.EstadoLegado? _estado;

    public string EnderecoServico { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSoapCore();
        builder.Services.AddSingleton<Fake.EstadoLegado>();
        builder.Services.AddSingleton<Fake.IServicoLegado, Fake.ServicoLegado>();

        _app = builder.Build();
        ((IApplicationBuilder)_app).UseSoapEndpoint<Fake.IServicoLegado>(
            "/ServicoLegado.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);

        await _app.StartAsync();

        _estado = _app.Services.GetRequiredService<Fake.EstadoLegado>();
        var enderecoBase = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        EnderecoServico = $"{enderecoBase}/ServicoLegado.svc";
    }

    public Task SimularIndisponibilidadeAsync(bool ativo)
    {
        (_estado ?? throw new InvalidOperationException("Fixture não inicializada.")).Indisponivel = ativo;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
