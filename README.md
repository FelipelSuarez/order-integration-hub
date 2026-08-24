# order-integration-hub

Integração de pedidos entre uma API moderna e um ERP legado via SOAP,
com mensageria resiliente e projeção de leitura separada.

> **Status:** em construção (ago/2026). Acompanhe as issues para o roadmap.

Stack: .NET 10 · SQL Server · MongoDB · MassTransit · Azure Service Bus · OpenTelemetry

## Estrutura

```
src/
  OrderIntake.Api/            Minimal API, composition root
  OrderIntake.Domain/         entidades, eventos, regras — zero dependência externa
  OrderIntake.Application/    casos de uso, portas (interfaces)
  OrderIntake.Infrastructure/ EF Core, cliente SOAP, MassTransit
  OrderProjection.Worker/     consome eventos, monta o read model
  OrderProjection.Api/        expõe a consulta
  Shared.Contracts/           contratos de evento versionados
tests/
  *.UnitTests / *.IntegrationTests
tools/
  OrderIntake.SeedData/       gera pedidos em volume pros testes de performance da S2
docs/
  adr/  domain.md
```

## Seed de dados

`tools/OrderIntake.SeedData` gera 100k+ pedidos direto no SQL Server, pros testes de
performance da S2. Não roda em CI nem em `dotnet test` — é sob demanda:

```bash
dotnet run --project tools/OrderIntake.SeedData -- "<connection-string>"
```

`OrderIntake.Domain` não referencia nada de infraestrutura — é verificável olhando
suas `ProjectReference` (nenhuma) e é uma restrição não-negociável (AGENTS.md).

## Pirâmide de testes

Sem meta de cobertura por número — cada teste existe pra provar um comportamento
específico, em três níveis:

- **Unitário** (`OrderIntake.UnitTests`) — regras do agregado `Pedido`: invariantes de
  registro, transições de `Status` válidas e inválidas. Sem I/O, sem mock de banco — o
  domínio é puro.
- **Integração** (`OrderIntake.IntegrationTests`, Testcontainers) — `RegistrarPedidoUseCase`
  e `PedidoRepository` contra SQL Server real, sem passar por HTTP; e o conflito de
  concorrência otimista (`rowversion`) acontecendo de verdade, não simulado.
- **Ponta a ponta** (`WebApplicationFactory`) — o contrato HTTP de `POST /pedidos` real:
  202 no caminho feliz, 400 no payload inválido, persistência confirmada no banco.

O que fica deliberadamente fora, por enquanto:

- **Integração SOAP com o legado** — o proxy ainda não existe (ZER-162).
- **Outbox/MassTransit** — a publicação transacional de eventos ainda não foi
  implementada (ADR-0003 documenta a decisão, não o código).
- **`OrderProjection`/MongoDB** — read model desnormalizado, fora do escopo desta leva
  de testes.
- **Carga e performance** — `tools/OrderIntake.SeedData` existe pra alimentar os testes
  de performance da S2; não é teste automatizado, é preparação de dados.
