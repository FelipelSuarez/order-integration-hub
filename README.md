# order-integration-hub

Integração de pedidos entre uma API moderna e um ERP legado via SOAP,
com mensageria resiliente e projeção de leitura separada.

> **Status:** em construção (ago/2026). Acompanhe as issues para o roadmap.

Stack: .NET 10 · SQL Server · MongoDB · MassTransit · Polly · Azure Service Bus ·
OpenTelemetry

O `OrderIntake` convive com dois protocolos ao mesmo tempo: expõe REST (`POST
/pedidos`) para quem integra com o hub, e fala SOAP como cliente do ERP legado
(`ILegadoPedidoGateway`, `OrderIntake.Infrastructure/Legado`) — a realidade de boa
parte das integrações corporativas .NET, não uma escolha de portfólio.

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
  LegadoErp.Fake/             simula o ERP legado via SOAP (SoapCore) — sobe no compose
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
- **Integração** (`OrderIntake.IntegrationTests`) — `RegistrarPedidoUseCase` e
  `PedidoRepository` contra SQL Server real (Testcontainers), sem passar por HTTP; o
  conflito de concorrência otimista (`rowversion`) acontecendo de verdade, não
  simulado; e `LegadoPedidoGatewayResilienceTests` contra o `LegadoErp.Fake` de
  verdade (Kestrel real dentro do processo de teste, sem mock) — aprovação, recusa de
  negócio que não abre o circuito, e o circuito abrindo/recuperando quando o legado
  fica indisponível (ADR-0006).
- **Ponta a ponta** (`WebApplicationFactory`) — o contrato HTTP de `POST /pedidos` real:
  202 no caminho feliz, 400 no payload inválido, persistência confirmada no banco.

O que fica deliberadamente fora, por enquanto:

- **O gateway SOAP conectado à saga** — `ILegadoPedidoGateway` existe e é testado
  isoladamente (ADR-0006), mas nenhum consumer o chama ainda; isso é escopo da
  ZER-183.
- **`OrderProjection`/MongoDB** — read model desnormalizado, fora do escopo desta leva
  de testes.
- **Carga e performance** — `tools/OrderIntake.SeedData` existe pra alimentar os testes
  de performance da S2; não é teste automatizado, é preparação de dados.
