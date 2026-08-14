# ADR-0002 — Consistência

## Contexto

Com dois serviços (ADR-0001) e uma fronteira orientada a eventos entre eles, é
preciso decidir o que é transacional e o que é eventual — e aceitar
formalmente a janela de inconsistência que resulta disso na projeção.

## Opções consideradas

1. **Consistência forte fim a fim** (transação distribuída entre
   `OrderIntake` e `OrderProjection`, ou `OrderIntake` escrevendo direto no
   MongoDB da projeção) — elimina a janela de leitura desatualizada, mas
   exige coordenação distribuída praticamente inviável entre SQL Server e
   MongoDB, ou fere a separação escrita/leitura da ADR-0001. Resolve um
   problema que o domínio de pedidos tolera por segundos.
2. **Eventual, mas publish direto sem outbox** — mais simples de implementar,
   sem tabela de outbox nem publisher em background. Mas publish e persist não
   são atômicos: se o processo cair entre os dois, o evento se perde e a
   projeção nunca sabe do pedido. É a classe de bug que o projeto existe para
   provar que sabe evitar.
3. **Transacional dentro do `OrderIntake`, eventual entre os serviços, via
   outbox** — escolhido.

## Decisão

Tudo que muda o estado do Pedido dentro do `OrderIntake` — criação, transições
de `Status` da saga — é transacional, e inclui o outbox na mesma transação SQL
Server: nenhum evento é publicado sem um estado persistido correspondente.
Tudo que atravessa a fronteira `OrderIntake → OrderProjection` é eventual — a
leitura aceita estar atrasada em relação à escrita, sem SLA de sincronização.

## Consequências

- Nenhum evento publicado "órfão" (sem persistência correspondente).
- Leitura desacoplada da escrita: escala e falha independentemente.
- **Negativa, aceita:** um cliente pode consultar o pedido logo após criá-lo e
  ver estado desatualizado — a projeção está sempre um passo atrás da
  escrita, por segundos ou mais se o broker/consumer cair.
- **Negativa, aceita:** a garantia é at-least-once, não exactly-once — todo
  consumer precisa ser idempotente (ADR-0007). Depurar "por que a projeção não
  atualizou" exige olhar broker e consumer, não só o banco de escrita.
