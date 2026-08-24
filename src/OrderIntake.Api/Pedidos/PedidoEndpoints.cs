using OrderIntake.Application.Pedidos;

namespace OrderIntake.Api.Pedidos;

public static class PedidoEndpoints
{
    public static WebApplication MapPedidoEndpoints(this WebApplication app)
    {
        app.MapPost("/pedidos", async (RegistrarPedidoRequest request, RegistrarPedidoUseCase useCase, CancellationToken cancellationToken) =>
        {
            var itens = request.Itens.Select(i => (i.ProdutoId, i.Quantidade)).ToList();
            var command = new RegistrarPedidoCommand(request.ClienteId, itens);

            try
            {
                var pedido = await useCase.ExecutarAsync(command, cancellationToken);
                var response = new RegistrarPedidoResponse(pedido.Id, pedido.Status.ToString());

                return Results.Accepted($"/pedidos/{pedido.Id}", response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { erro = ex.Message });
            }
        });

        return app;
    }
}
