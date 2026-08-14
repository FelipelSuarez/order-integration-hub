# Domínio — Hub de Integração de Pedidos

## Linguagem ubíqua

- **Pedido** — agregado raiz. Um conjunto de Itens submetido por um Cliente. Tem um
  `Status` que evolui conforme o legado confirma dados e disponibilidade.
- **Item** — produto + quantidade dentro de um Pedido. Não existe fora de um Pedido.
- **Cliente** — referência ao cadastro do legado (`ClienteId`). O hub não duplica o
  cadastro, só valida a referência.
- **Status** — `Recebido → Validado → Confirmado` (sucesso) ou `Recebido/Validado →
  Rejeitado` (falha, com motivo). Terminal: `Confirmado` ou `Rejeitado`.
- **Reserva de Estoque** — resultado da consulta ao legado confirmando quantidade
  disponível para cada Item do Pedido. Não é decidida pelo hub; é reportada por ele.

## Eventos de domínio

O fluxo é uma saga de dois passos dentro do próprio `OrderIntake` — sem terceiro
serviço (ADR-0001). Cada passo reage ao evento anterior via consumer do MassTransit.

1. **`PedidoRecebido`** — Pedido persistido com `Status = Recebido`. Dispara a
   validação contra o legado (cliente existe, itens existem).
2. **`PedidoValidado`** — legado confirmou cliente e itens. `Status = Validado`.
   Dispara a reserva de estoque.
3. **`EstoqueReservado`** — legado confirmou quantidade disponível para todos os
   itens. `Status = Confirmado`. Terminal, sucesso.
4. **`PedidoRejeitado`** — legado recusou em qualquer um dos dois passos acima
   (cliente/item inválido, ou estoque insuficiente). Carrega o motivo. `Status =
   Rejeitado`. Terminal, falha.

Cada consumer é idempotente (ADR-0007): reprocessar `PedidoRecebido` ou
`PedidoValidado` não pode gerar reserva ou rejeição duplicada.

## Fronteira de consistência

**Transacional** (mesma transação SQL Server, outbox incluso — ADR-0003):
- Criação do Pedido + Itens + outbox de `PedidoRecebido`.
- Cada transição de `Status` + outbox do evento correspondente (`PedidoValidado`,
  `EstoqueReservado` ou `PedidoRejeitado`).

A chamada SOAP ao legado acontece **antes** da transação, fora dela — é consulta,
não escrita distribuída. O resultado da chamada decide qual evento a transação grava.

**Eventual:**
- Cada salto broker → consumer dentro da própria saga do `OrderIntake` (o hub fica
  em `Recebido` ou `Validado` por um tempo indeterminado enquanto aguarda o legado).
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
    description: Pedido aceito, processamento é assíncrono (Status = Recebido)
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
