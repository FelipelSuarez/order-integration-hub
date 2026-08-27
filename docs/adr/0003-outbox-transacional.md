# ADR-0003 — Outbox transacional

## Contexto

A ADR-0002 já decidiu que a fronteira `OrderIntake → OrderProjection` é eventual, mas
que a escrita do `Pedido` e a publicação do evento correspondente precisam ser
atômicas: nenhum evento pode ser publicado sem um estado persistido, e nenhum estado
persistido pode "esquecer" de publicar seu evento. Faltava decidir *como* implementar
essa atomicidade — o mecanismo, não o princípio.

O caso concreto: `PedidoRepository.AdicionarAsync` grava `Pedido` + `Itens` numa
transação SQL Server explícita (ADR-0004). O evento `PedidoRecebido` precisa nascer
dentro dessa mesma transação, sem depender de o broker estar disponível no instante do
commit.

## Opções consideradas

1. **Outbox artesanal** — tabela própria (`OutboxMessages`) gravada na mesma transação
   via EF Core, e um `BackgroundService` de fabricação própria fazendo polling e
   publicando. Funciona, mas reimplementa manualmente entrega at-least-once, lock de
   linha entre múltiplas instâncias do processo e retry — problemas que o MassTransit já
   resolve para o transporte que ele mesmo gerencia.
2. **Publish direto, sem outbox** — já descartado na ADR-0002: publish e persist não são
   atômicos, então uma falha entre os dois perde o evento silenciosamente.
3. **Transactional Outbox nativo do MassTransit sobre EF Core**
   (`AddEntityFrameworkOutbox<OrderIntakeDbContext>` + `UseBusOutbox()`) — escolhida.
   `IPublishEndpoint.Publish` chamado dentro do mesmo `DbContext`/transação grava na
   tabela `OutboxMessage` em vez de ir para a rede; um `BusOutboxDeliveryService`
   (hosted service do próprio MassTransit) faz o polling e a entrega real ao broker,
   com retry e concorrência entre instâncias já resolvidos pela biblioteca.

## Decisão

Usar o outbox nativo do MassTransit sobre EF Core. `OrderIntakeDbContext` ganha as
entidades `OutboxMessage` e `OutboxState` (`modelBuilder.AddOutboxMessageEntity()` /
`AddOutboxStateEntity()`), criadas por migration. `PedidoRepository.AdicionarAsync`
chama `IPublishEndpoint.Publish(new PedidoRecebido(...))` **antes** de
`SaveChangesAsync`, ainda dentro da transação explícita já existente (ADR-0004) — é
esse posicionamento que faz o publish virar uma escrita na tabela de outbox, não uma
chamada de rede.

Contratos de evento vivem em `Shared.Contracts`, versionados por namespace
(`Shared.Contracts.Pedidos.V1`, `V2`...). Um campo aditivo/opcional não quebra o
contrato e não exige nova versão; remoção, renomeação ou troca de tipo de um campo
existente exige um novo namespace de versão, com o tipo antigo mantido até todos os
consumers migrarem.

## Consequências

- Nenhum evento publicado "órfão": outbox e escrita do `Pedido` compartilham a mesma
  transação, commit ou rollback juntos.
- **Negativa, aceita:** a entrega ao broker não é instantânea — depende do polling do
  `BusOutboxDeliveryService` em background. Isso é a prova em código da janela de
  inconsistência que a ADR-0002 já aceitou, não uma regressão.
- **Negativa, aceita:** três tabelas técnicas a mais no banco de escrita
  (`OutboxMessage`, `OutboxState`, `InboxState` — esta última existe porque
  `OutboxMessage` tem uma FK opcional pra ela, parte do modelo compartilhado do
  MassTransit), migradas e mantidas junto com o schema de domínio.
- `InboxState` não é usada para dedupe de consumer nesta ticket — ver ADR-0007 para a
  estratégia de idempotência escolhida no lado do consumer.
