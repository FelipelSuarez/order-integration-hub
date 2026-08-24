# ADR-0004 — Isolamento e concorrência na escrita do Pedido

## Contexto

A escrita do `Pedido` no `OrderIntake` acontece em dois momentos distintos, com riscos
de concorrência diferentes:

1. **Criação** — `Pedido` + `Itens` são inseridos numa única transação (ADR-0002). A
   chamada ao legado que decide o que será gravado já aconteceu *antes*, fora dessa
   transação — não há leitura seguida de decisão dentro dela, só um INSERT.
2. **Transições de `Status`** — a saga (`Recebido → Validando → Reservado | Rejeitado`,
   ZER-162) lê um `Pedido` existente, decide a próxima transição e grava. Sob entrega
   at-least-once (ADR-0007), dois processamentos do mesmo evento — ou dois consumers
   duplicados por engano — podem ler o mesmo `Pedido` e tentar escrever a partir do
   mesmo estado, um sobrescrevendo o outro sem perceber.

Era preciso decidir conscientemente o nível de isolamento da transação de escrita, em
vez de aceitar por omissão o que a connection string traz.

## Opções consideradas

1. **READ COMMITTED explícito** — o default do SQL Server, mas fixado no código
   (`BeginTransactionAsync(IsolationLevel.ReadCommitted)`) em vez de implícito.
   Suficiente para a transação de criação, que é um INSERT sem invariante
   read-then-write. Não resolve sozinho o risco de escrita concorrente das transições de
   Status — precisa de outro mecanismo para isso.
2. **SNAPSHOT / Read Committed Snapshot Isolation (RCSI)** — reduz bloqueio entre
   leitores e escritores via versionamento de linha no tempdb. Relevante quando há
   leitura concorrente pesada durante a escrita; não é o caso aqui — o volume de escrita
   é baixo e não há relatório/consulta pesada competindo pela mesma linha no momento da
   criação.
3. **SERIALIZABLE** — a garantia mais forte, evita phantom reads. Só se justificaria se
   houvesse uma checagem tipo "não duplicar este pedido" feita dentro da própria
   transação de escrita — o que não existe no escopo atual (deduplicação de submissões
   fica para uma decisão futura, se necessário).

## Decisão

**Criação:** READ COMMITTED explícito. A transação só insere; não há nada para o
isolamento mais forte proteger que ele já não proteja.

**Transições de Status:** concorrência otimista via `rowversion` (SQL Server
`ROWVERSION`/`TIMESTAMP`), mapeado como shadow property no EF Core
(`PedidoConfiguration.IsRowVersion()`), sem expor o conceito no domínio. Cada
`SaveChanges` inclui a `RowVersion` lida no `WHERE` do `UPDATE`; se ela não bater mais
(alguém já escreveu por cima), o EF Core lança `DbUpdateConcurrencyException` em vez de
sobrescrever silenciosamente. Isolamento de transação não resolve esse problema —
otimismo por versão de linha resolve, e é mais barato que lock pessimista para o volume
de escrita esperado.

## Consequências

- Nenhuma escrita de transição de Status sobrescreve outra silenciosamente: o conflito
  vira uma exceção observável, testada em
  `PedidoConcorrenciaTests.TransicaoConcorrenteDeStatusLancaConflito`.
- `DbUpdateConcurrencyException` precisa de um handler explícito em quem chama o
  repositório (o consumer da saga, ZER-162) — hoje ela simplesmente propaga. Decidir a
  política de retry/descarte fica para aquela ticket.
- **Negativa, aceita:** não protege contra pedidos duplicados criados concorrentemente
  (duas submissões quase simultâneas do mesmo cliente/itens geram dois `Pedido`
  distintos, ambos válidos). Não é o problema que esta ADR resolve; se motivo virar
  encontrado, é uma decisão de deduplicação/idempotência na entrada, não de isolamento
  de transação.
- **Negativa, aceita:** RCSI não foi habilitado no banco. Se o volume de leitura
  concorrente crescer a ponto de gerar bloqueio real, essa decisão precisa ser revisada.
