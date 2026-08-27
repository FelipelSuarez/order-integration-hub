# ADR-0007 — Idempotência do consumer

## Contexto

Entrega via broker é at-least-once (consequência aceita na ADR-0002): o mesmo
`PedidoRecebido` pode chegar duas vezes ao `PedidoRecebidoConsumer` — reentrega depois
de falha de ack, redeploy no meio do processamento, ou reprocessamento manual. O
consumer aplica `Pedido.IniciarValidacao()`, uma transição `Recebido → Validando`.
Processar a mesma mensagem duas vezes não pode gerar um erro não tratado nem um efeito
duplicado.

## Opções consideradas

1. **`InboxState` do MassTransit** — dedupe automático por `MessageId`, mantendo uma
   tabela de mensagens já processadas por endpoint de recebimento (a tabela já existe no
   schema, ADR-0003, mas configurar o *pipeline* de dedupe é opt-in por endpoint).
   Genérico e correto para qualquer efeito, inclusive não-idempotente, mas é
   configuração extra por endpoint para um efeito que já é idempotente por natureza.
2. **At-most-once (desabilitar redelivery/retry)** — mais simples, mas joga fora a
   garantia de entrega que o outbox (ADR-0003) existe para dar. Não é uma troca
   aceitável: perderíamos mensagem em vez de duplicar efeito.
3. **Idempotência por guarda de estado do domínio** — o consumer lê o `Pedido`, e só
   chama `IniciarValidacao()` se `Status == Recebido`; caso contrário retorna sem
   efeito. Escolhida.

## Decisão

`PedidoRecebidoConsumer` busca o `Pedido` pelo `PedidoId` da mensagem e verifica o
`Status` antes de agir: se já não estiver em `Recebido` (ou seja, a mensagem já foi
processada antes, ou o pedido não existe mais), o consumer retorna sem erro e sem
tentar a transição de novo. A idempotência vem da própria máquina de estados do
`Pedido` (`docs/domain.md`), que já rejeita transições inválidas por construção
(`Pedido.IniciarValidacao()` lança se `Status != Recebido`) — não de dedupe por
`InboxState`.

A tabela `InboxState` existe no schema (`OutboxMessage` tem uma FK opcional pra ela,
parte do modelo do outbox — ADR-0003), mas não é usada: nenhum consumer está
configurado pra gravar nela, é um artefato inerte do modelo compartilhado do
MassTransit.

A guarda de `Status` sozinha não fecha a janela entre duas entregas **concorrentes**
(não apenas sequenciais): ambas podem ler `Status == Recebido` antes de qualquer uma
salvar. Quem perde a corrida esbarra no `rowversion` otimista (ADR-0004) e recebe
`DbUpdateConcurrencyException` — o consumer trata esse conflito especificamente como o
mesmo no-op, não como falha a reprocessar/mandar pra fila de erro.

## Consequências

- Reprocessar `PedidoRecebido` é seguro, sequencial ou concorrente: primeira entrega (a
  que ganha a corrida, se houver disputa) aplica a transição; qualquer entrega repetida
  é um no-op observável, via guarda de `Status` ou via `DbUpdateConcurrencyException`
  capturada — nunca uma exceção não tratada indo pra fila de erro do broker.
- **Negativa, aceita:** essa estratégia só funciona porque o efeito é uma transição de
  estado idempotente por construção. Um efeito colateral não-idempotente — por exemplo,
  a chamada SOAP de reserva de estoque que a ZER-183 vai adicionar — não pode reusar a
  mesma guarda; vai precisar de uma estratégia própria (possivelmente passar a usar o
  `InboxState` que já existe no schema, ou dedupe explícito por `MessageId`).
- Sem dedupe ativo, não há histórico de "mensagens já vistas" para auditoria — só o
  estado atual do `Pedido`. Suficiente para o problema atual; insuficiente se um dia
  precisarmos provar quantas vezes uma mensagem específica foi entregue.
