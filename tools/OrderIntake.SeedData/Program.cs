using System.Diagnostics;
using Bogus;
using Microsoft.EntityFrameworkCore;
using OrderIntake.Domain.Pedidos;
using OrderIntake.Infrastructure.Persistence;

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/OrderIntake.SeedData -- \"<connection-string>\"");
    return 1;
}

const int totalPedidos = 100_000;
const int tamanhoDoLote = 1_000;

var connectionString = args[0];

var options = new DbContextOptionsBuilder<OrderIntakeDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using (var setupContext = new OrderIntakeDbContext(options))
{
    await setupContext.Database.MigrateAsync();
}

var faker = new Faker();
var cronometro = Stopwatch.StartNew();
var inseridos = 0;

while (inseridos < totalPedidos)
{
    var tamanhoDoLoteAtual = Math.Min(tamanhoDoLote, totalPedidos - inseridos);

    await using var context = new OrderIntakeDbContext(options);

    for (var i = 0; i < tamanhoDoLoteAtual; i++)
    {
        await context.Pedidos.AddAsync(GerarPedido(faker));
    }

    await context.SaveChangesAsync();
    inseridos += tamanhoDoLoteAtual;

    Console.WriteLine($"{inseridos}/{totalPedidos} pedidos inseridos ({cronometro.Elapsed:mm\\:ss})");
}

cronometro.Stop();
Console.WriteLine($"Concluído: {totalPedidos} pedidos em {cronometro.Elapsed:mm\\:ss}.");

return 0;

static Pedido GerarPedido(Faker faker)
{
    var quantidadeDeItens = faker.Random.Int(1, 5);
    var itens = new List<(Guid ProdutoId, int Quantidade)>(quantidadeDeItens);

    for (var i = 0; i < quantidadeDeItens; i++)
    {
        itens.Add((Guid.NewGuid(), faker.Random.Int(1, 20)));
    }

    var pedido = Pedido.Registrar(Guid.NewGuid(), itens);

    switch (faker.Random.Int(0, 3))
    {
        case 1:
            pedido.IniciarValidacao();
            break;
        case 2:
            pedido.IniciarValidacao();
            pedido.ConfirmarReserva();
            break;
        case 3:
            pedido.IniciarValidacao();
            pedido.Rejeitar(faker.Lorem.Sentence());
            break;
    }

    return pedido;
}
