# Domínio — Hub de Integração de Pedidos

## Linguagem ubíqua

- **Pedido** — agregado raiz. Um conjunto de Itens submetido por um Cliente. Tem um
  `Status` que evolui conforme o legado confirma dados e disponibilidade.
- **Item** — produto + quantidade dentro de um Pedido. Não existe fora de um Pedido.
- **Cliente** — referência ao cadastro do legado (`ClienteId`). O hub não duplica o
  cadastro, só valida a referência.
- **Status** — estados da saga, no máximo 4: `Recebido → Validando →
  Reservado | Rejeitado`. Terminal: `Reservado` (sucesso) ou `Rejeitado` (falha,
  com motivo).
- **Reserva de Estoque** — resultado da consulta ao legado confirmando quantidade
  disponível para cada Item do Pedido. Não é decidida pelo hub; é reportada por ele.

## Eventos de domínio

O fluxo é uma saga interna dentro do próprio `OrderIntake` — sem terceiro serviço
(ADR-0001) e sem SOAP no request path: a API só persiste e responde; um consumer
do MassTransit processa a validação depois.

1. **`PedidoRecebido`** — Pedido persistido com `Status = Recebido`. Dispara o
   consumer que entra em `Validando` e chama o legado (cliente, itens, estoque).
2. **`PedidoValidado`** — legado confirmou cliente e itens.
3. **`EstoqueReservado`** — legado confirmou quantidade disponível para todos os
   itens. `Status = Reservado`. Terminal, sucesso.
4. **`PedidoRejeitado`** — legado recusou (cliente/item inválido, estoque
   insuficiente, ou timeout — um único timeout de N minutos sem resposta do
   legado). Carrega o motivo. `Status = Rejeitado`. Terminal, falha.

`PedidoValidado` e `EstoqueReservado` são emitidos na mesma passagem por
`Validando` — não viram estados próprios da state machine, só eventos, para
manter os 4 estados no máximo. Cada consumer é idempotente
(ADR-0007): reprocessar `PedidoRecebido` não pode gerar reserva ou rejeição
duplicada.

## Fronteira de consistência

**Transacional** (mesma transação SQL Server, outbox incluso — ADR-0003):
- Criação do Pedido + Itens + outbox de `PedidoRecebido`.
- Cada transição de `Status` da saga (`EntityFrameworkSagaRepository`) + outbox
  dos eventos correspondentes, na mesma transação.

A chamada SOAP ao legado acontece **antes** da transação, fora dela — é consulta,
não escrita distribuída. O resultado da chamada decide o que a transação grava.

**Eventual:**
- O salto broker → consumer que dispara `Validando` (o pedido fica em `Recebido`
  por um tempo indeterminado, ou até o timeout, enquanto aguarda o legado).
- O modelo de leitura do `OrderProjection`, sempre atrasado em relação à escrita.

## Contrato REST de entrada

```yaml
POST /pedidos
requestBody:
  application/json:
    clienteId: string   # obrigatório
    itens:
      - produtoId: string
        quantidade: integer   # > 0
  required: [clienteId, itens]   # itens: mínimo 1
responses:
  202:
    description: Pedido aceito, processamento é assíncrono (Status = Recebido).
      Não há aceite síncrono — quem consome recebe 202 e precisa consultar o
      status (custo real de contrato, ver ADR-0011).
    headers:
      Location: /pedidos/{pedidoId}   # consulta de status
    body: { pedidoId: guid, status: "Recebido" }
  400:
    description: payload inválido (não confunda com PedidoRejeitado, que é regra
      de negócio validada pelo legado)
```

Consulta de status é responsabilidade do `OrderProjection` (`GET` sobre o read
model), não do `OrderIntake`.

## Regra

Nenhuma feature entra se não for necessária para demonstrar falha, concorrência ou
estado. CRUD não convence ninguém em nível sênior.
