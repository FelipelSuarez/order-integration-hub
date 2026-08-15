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
docs/
  adr/  domain.md
```

`OrderIntake.Domain` não referencia nada de infraestrutura — é verificável olhando
suas `ProjectReference` (nenhuma) e é uma restrição não-negociável (AGENTS.md).
