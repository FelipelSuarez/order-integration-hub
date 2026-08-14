# AGENTS.md

Hub de integração de pedidos em .NET 10. Uma API moderna recebe pedidos, valida contra
um ERP legado que só fala SOAP, persiste transacionalmente e publica eventos. Um segundo
serviço consome esses eventos e mantém um modelo de leitura desnormalizado.

Este projeto é um portfólio técnico. Isso muda as prioridades: **clareza de decisão vale
mais que quantidade de código, e o histórico do repositório faz parte do produto.**

## Comandos

```bash
dotnet build                      # build da solution
dotnet test                       # todos os testes (requer Docker)
dotnet test tests/OrderIntake.UnitTests   # só unitários, rápido
docker compose up                 # ambiente completo
dotnet ef migrations add <Nome> --project src/OrderIntake.Infrastructure
```

Testes de integração sobem SQL Server, MongoDB e RabbitMQ via Testcontainers. Docker
precisa estar rodando.

## Arquitetura — restrições que não se negociam

Estas regras existem por decisão documentada. Antes de violar qualquer uma, leia a ADR
correspondente em `docs/adr/` e proponha a mudança em vez de contorná-la.

- **`OrderIntake.Domain` não referencia nada de infraestrutura.** Sem EF Core, sem
  MassTransit, sem `System.Net`. Se uma regra de domínio precisa de I/O, o desenho está
  errado — a dependência entra por interface definida em `Application`.
- **Nenhum evento é publicado diretamente.** Toda publicação passa pelo outbox
  transacional do MassTransit (ADR-0003). `IPublishEndpoint` chamado fora de uma
  transação é bug.
- **`OrderProjection` nunca escreve no SQL Server.** Ele só lê eventos e escreve no
  MongoDB. A separação de leitura e escrita é o ponto do projeto (ADR-0002).
- **Nada do contrato SOAP gerado atravessa a camada `Application`.** O proxy do legado
  fica em `Infrastructure` e é traduzido para tipos de domínio ali (ADR-0006).
- **Consumidores são idempotentes.** A entrega é at-least-once; processar duas vezes não
  pode duplicar efeito (ADR-0007).

## Código

- .NET 10, C# 14. Nullable habilitado, warnings tratados como erro — não silencie com
  `#pragma` ou `!` sem justificar em comentário.
- Mapeamento EF Core explícito via `IEntityTypeConfiguration`, nunca por convenção.
- Nomes de domínio em português quando forem termos do negócio (`Pedido`, `Reserva`),
  nomes técnicos em inglês. A linguagem ubíqua está em `docs/domain.md`.
- Não adicione pacotes NuGet sem perguntar antes. Cada dependência é uma decisão.

## Testes

- Domínio: unitários puros, sem mock de banco.
- Casos de uso e repositórios: integração com Testcontainers contra SQL Server real.
- **Nunca use o provider InMemory do EF Core** (ADR-0008). Ele mente sobre o
  comportamento do banco e esconde exatamente os bugs que interessam.
- Ao alterar comportamento, atualize ou acrescente teste — mesmo que não tenha sido
  pedido.
- Bugs de concorrência ou de mensageria precisam de teste que reproduza a falha antes da
  correção.

## Commits e PRs

O histórico deste repositório é lido por avaliadores. Ele é parte do portfólio.

- **Commits atômicos.** Uma mudança coerente por commit. Nunca agrupe uma feature inteira
  num commit só, mesmo que o trabalho tenha sido feito de uma vez.
- Conventional Commits, mensagem no imperativo: `feat: adiciona outbox ao OrderIntake`.
- Nada de `wip`, `ajustes`, `fix`.
- Trabalhe em branch e abra PR com descrição do que muda e por quê. `main` fica sempre
  verde.
- Rode `dotnet build` e `dotnet test` antes de commitar.

## Decisões de arquitetura

Toda decisão estrutural vira uma ADR em `docs/adr/`, no formato contexto → opções
consideradas → decisão → consequências. Inclua as consequências negativas.

Se uma tarefa exigir uma escolha entre alternativas com trade-off real, **pare e sinalize
em vez de decidir sozinho** — a decisão e o registro dela são o entregável principal deste
projeto, não o código.

Nunca apague uma ADR existente. Se ela for revista, marque como substituída e escreva a
nova.

## Segurança

- **Nenhum segredo no repositório.** Ele é público desde o commit 1; um vazamento fica no
  histórico para sempre.
- Connection strings e credenciais vêm de `dotnet user-secrets` em desenvolvimento e de
  variáveis de ambiente no compose.
- `appsettings.json` só contém configuração não sensível.
- Não commite dumps de banco, arquivos de trace ou saída de log — podem conter dados.

## O que não fazer

- Não introduza um terceiro serviço. Dois é uma decisão deliberada (ADR-0001).
- Não adicione feature de CRUD que não demonstre falha, concorrência ou estado.
- Não gere código de cliente SOAP na mão; use `dotnet-svcutil` e mantenha o gerado fora
  do controle de versão manual.
- Não altere `docs/adr/` sem que a mudança de código correspondente exista.