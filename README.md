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
