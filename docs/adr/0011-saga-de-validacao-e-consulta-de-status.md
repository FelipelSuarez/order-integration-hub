# ADR-0011 — Saga de validação do Pedido e consulta de status interina

## Contexto

`PedidoRecebidoConsumer` (ZER-161) só fazia `Recebido → Validando`, sem chamar o
legado — a metade que faltava é o ponto central da ZER-183: sem uma saga que realmente
decide `Reservado`/`Rejeitado` a partir do legado, toda a resiliência SOAP da ZER-162
(Polly, `LegadoIndisponivelException`) é decorativa — o circuit breaker abriria e a
única consequência seria... nada, porque nada consome o resultado.

Três decisões, tomadas juntas porque são a mesma peça de trabalho.

## Opções consideradas — onde mora o estado da saga

1. **`Pedido` (Domain) também é a saga instance** — mais simples, uma tabela só. Mas
   `SagaStateMachineInstance` é tipo do MassTransit (infraestrutura); `Pedido` não pode
   implementá-lo sem violar a regra não-negociável do AGENTS.md
   (`OrderIntake.Domain` zero dependência de infra).
2. **`PedidoSagaState`, tabela própria, espelhando o status em paralelo** —
   escolhida. `CurrentState` é bookkeeping do MassTransit (decide qual handler da state
   machine dispara); `Pedido.Status` continua sendo o dado de negócio, gravado pelos
   métodos de domínio já existentes (`IniciarValidacao()`, `ConfirmarReserva()`,
   `Rejeitar(motivo)`) via `IPedidoRepository`, no mesmo `DbContext`/transação. Os dois
   ficam paralelos de propósito — não é duplicação por descuido.

## Opções consideradas — legado indisponível durante Validando

Já alinhado antes de implementar: quando `ILegadoPedidoGateway` lança
`LegadoIndisponivelException` (circuito aberto ou falha técnica persistente —
ADR-0006), a saga **não rejeita**. Duas formas de não rejeitar:

1. **Reverter `Validando → Recebido`** — mais fiel à frase original do ticket da
   ZER-162 ("pedido parado em Recebido"), mas cria uma transição/evento que
   `docs/domain.md` não lista, e reprocessar como se fosse a primeira vez complica a
   idempotência.
2. **Permanecer em `Validando`, reagendar uma reavaliação** — escolhida. Cabe nos 4
   estados já decididos; `PedidoSagaOptions.OrcamentoTotal` (padrão: 15 min) é o
   orçamento total desde a primeira tentativa — dentro dele, reagenda
   (`context.ScheduleSend`); esgotado, rejeita com motivo técnico. Implementa
   literalmente "timeout: legado não respondeu em N minutos → rejeita ou reenfileira"
   do ticket original.

## Opções consideradas — mecanismo de agendamento

1. **`UseDelayedMessageScheduler()` do MassTransit sobre RabbitMQ** — escolhida, mas
   com uma pegadinha descoberta rodando os testes: precisa do plugin
   `rabbitmq_delayed_message_exchange` no broker (exchange `x-delayed-message`), que
   **não** vem na imagem oficial `rabbitmq:4-management`. Sem ele, o próprio consumer
   falha ao abrir conexão (`PRECONDITION_FAILED - unknown exchange type`).
   `docker-compose.yml` e `RabbitMqContainerFixture` passaram a usar
   `heidiks/rabbitmq-delayed-message-exchange:4.2.0-management` — mesmo RabbitMQ 4.x,
   plugin pré-habilitado. Sem pacote NuGet novo; mantém agendamento assíncrono de
   verdade (não segura conexão/slot do consumer esperando).
2. **`UseMessageRetry` com intervalos, sem scheduler** — descartada. Evita mexer na
   imagem do RabbitMQ, mas o retry do MassTransit segura a mensagem (e o slot do
   consumer) durante toda a espera entre tentativas — minutos de um consumer ocupado
   por Pedido, ao invés de reagendamento assíncrono de verdade.
3. **Quartz.NET** — descartada por hora. Resolveria sem tocar na imagem do RabbitMQ,
   mas é uma dependência nova (`MassTransit.Quartz`), decisão que este projeto trata
   como não-trivial (AGENTS.md: pacote novo é decisão, não default).

## Opções consideradas — idempotência da chamada ao legado

ADR-0007 já previa isso: a guarda por `Status` que idempotentiza
`Recebido → Validando` não serve para um efeito colateral **não-idempotente** — a
reserva de estoque em si. Reentrega de `ReavaliarPedido` enquanto o Pedido ainda está
em `Validando` chamaria o legado de novo, arriscando reserva duplicada.

Escolhida a opção que ADR-0007 já apontava: dedupe por `MessageId` via `InboxState`
(existe no schema desde a ZER-161, inerte até aqui) —
`x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<OrderIntakeDbContext>(context))`
habilita o inbox em todo endpoint auto-configurado, sem exigir endpoint explícito só
para a saga.

## Opções consideradas — consulta de status

`docs/domain.md` já decidiu: consulta de status é responsabilidade do
`OrderProjection` (`GET` sobre o read model), não do `OrderIntake`. Mas o
`OrderProjection` ainda não existe (ZER-163, backlog) — sem *nenhum* jeito de ver o
resultado da saga de fora, o `POST /pedidos` vira uma caixa preta.

1. **Esperar a ZER-163** — mais correto arquiteturalmente, mas deixa a ZER-183 sem
   forma de demonstrar o que ela constrói.
2. **`GET /pedidos/{id}` interino no próprio `OrderIntake`** — escolhida. Lê
   `IPedidoRepository.ObterPorIdAsync` (já existe), 404 se não achar. Interino de
   propósito: quando o `OrderProjection` existir, este endpoint é descontinuado — não é
   o modelo de leitura definitivo, é só visibilidade enquanto ele não existe.

## Decisão

`PedidoValidacaoStateMachine` (`OrderIntake.Infrastructure/Sagas`) substitui
`PedidoRecebidoConsumer`: `Initially(When(PedidoRecebido))` transiciona
`Recebido → Validando` e chama o legado; aprovado publica `PedidoValidado` +
`EstoqueReservado` e vai para `Reservado`; recusa de negócio publica
`PedidoRejeitado(motivo)` e vai para `Rejeitado`; legado indisponível permanece em
`Validando` e reagenda via `ScheduleSend`, dentro do orçamento de
`PedidoSagaOptions.OrcamentoTotal`. `GET /pedidos/{id}` expõe o resultado
enquanto o `OrderProjection` não existe.

## Consequência de contrato

A API nunca deu aceite síncrono (`POST /pedidos` sempre retornou `202`, desde a
ZER-158) — mas antes da ZER-183 isso era teórico, porque nada decidia
`Reservado`/`Rejeitado` de verdade. Agora é custo real: quem integra com o hub recebe
`202` e precisa consultar `GET /pedidos/{id}` depois, possivelmente repetidas vezes se
o legado estiver lento ou indisponível — não há caminho síncrono de volta.

## Consequências

- `PedidoSagaState` e `Pedido.Status` só ficam inconsistentes entre si por uma janela
  muito curta (mesma transação, na prática) — mas são duas fontes, não uma; um bug
  futuro que só atualize uma das duas passaria despercebido sem teste.
- **Negativa, aceita:** `heidiks/rabbitmq-delayed-message-exchange` é imagem mantida
  por terceiro, não pelo time oficial do RabbitMQ — risco de supply chain aceito
  conscientemente por não exigir pacote NuGet novo nem bloquear o consumer segurando
  slot.
- **Negativa, aceita:** `GET /pedidos/{id}` interino lê do lado de escrita (SQL
  Server), não de um read model desnormalizado — sob carga, compete com a escrita pelo
  mesmo banco. Aceitável para o volume de tráfego atual (zero em produção); descontinuar
  quando o `OrderProjection` (ZER-163) existir.
- **Negativa, aceita:** o orçamento de retentativa (`PedidoSagaOptions.OrcamentoTotal`,
  padrão 15 min) é por-Pedido, não global — muitos Pedidos represados simultaneamente
  durante uma indisponibilidade prolongada do legado geram muitas mensagens
  `ReavaliarPedido` reagendadas em paralelo; não há um limite agregado de tentativas
  concorrentes.
