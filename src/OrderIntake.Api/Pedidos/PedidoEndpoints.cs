using OrderIntake.Application.Pedidos;

namespace OrderIntake.Api.Pedidos;

public static class PedidoEndpoints
{
    public static WebApplication MapPedidoEndpoints(this WebApplication app)
    {
        app.MapPost("/pedidos", async (RegistrarPedidoRequest request, RegistrarPedidoUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (request.Itens is null || request.Itens.Any(i => i is null))
            {
                return Results.BadRequest(new { erro = "Itens não pode ser nulo nem conter itens nulos." });
            }

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

        // Interino (ADR-0011): consulta de status é, por design, responsabilidade do
        // OrderProjection (docs/domain.md) — mas esse serviço ainda não existe (ZER-163).
        // Enquanto isso, lê direto do lado de escrita pra não deixar a saga sem visibilidade
        // nenhuma de fora.
        app.MapGet("/pedidos/{id:guid}", async (Guid id, IPedidoRepository repository, CancellationToken cancellationToken) =>
        {
            var pedido = await repository.ObterPorIdAsync(id, cancellationToken);

            return pedido is null
                ? Results.NotFound()
                : Results.Ok(new ConsultarPedidoResponse(pedido.Id, pedido.Status.ToString(), pedido.MotivoRejeicao));
        });

        return app;
    }
}
