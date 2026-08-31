# ADR-0006 — Proxy SOAP do legado e resiliência (Polly)

## Contexto

O ERP legado só fala SOAP. Faltava decidir três coisas antes de escrever código:
como simular esse legado para desenvolvimento/teste sem depender de um ambiente real;
como isolar o cliente SOAP gerado do resto do hub (AGENTS.md proíbe qualquer tipo de
`System.ServiceModel` atravessando `Application`); e como o hub reage quando o legado
está fora do ar, distinguindo isso de uma recusa de negócio legítima.

Este ADR não cobre a orquestração da saga que decide `Reservado`/`Rejeitado` a partir
da resposta do legado — isso é escopo da ZER-183, que ainda não existe. A porta e o
adapter aqui construídos ficam prontos, mas isolados: nenhum consumer os chama ainda.

## Opções consideradas — serviço fake do legado

1. **CoreWCF self-host** — mais fiel a um legado real em .NET Framework (bindings,
   `ServiceHost`, contratos WCF completos). Mas soma uma segunda incógnita de
   compatibilidade com .NET 10 à que já existia do lado cliente, e o objetivo aqui é
   simular o *comportamento* do legado (aprova, recusa, cai fora do ar), não reproduzir
   a stack de hospedagem WCF em si.
2. **SoapCore sobre ASP.NET Core** — escolhida. Expõe um endpoint SOAP/WSDL a partir de
   um serviço C# comum (`[ServiceContract]`/`[OperationContract]`), hospedado como
   qualquer app ASP.NET Core. Compatibilidade com .NET 10 tranquila (não depende de
   `System.ServiceModel` do lado servidor), o que concentra o risco técnico real do
   ticket — o cliente gerado — num único lugar.

## Opções consideradas — cliente SOAP

1. **Cliente escrito à mão** contra o WSDL — descartado por instrução direta do
   AGENTS.md: não se escreve cliente SOAP na mão.
2. **`dotnet-svcutil` gerando contra `System.ServiceModel.Primitives`/`.Http`** —
   escolhida. `System.ServiceModel.Primitives`/`.Http` publicaram build `10.0.652802`
   compatível com .NET 10, resolvendo a maior incógnita técnica que o ticket
   apontava. O `ClientBase<T>` gerado (`Legado/Gerado/ServiceReference/Reference.cs`)
   nunca é editado à mão — regenerar é a única forma de alterá-lo.
   `System.Security.Cryptography.Xml` fica pinado em `10.0.11` no
   `OrderIntake.Infrastructure.csproj` porque a transitiva puxada por
   `System.ServiceModel.Http 10.0.652802` (`10.0.0`) tem CVE conhecida
   (GHSA-23rf-6693-g89p, entre outras).

## Opções consideradas — sinalização de falha

1. **Uma única exceção para tudo** (recusa de negócio e indisponibilidade técnica
   ambas viram exceção) — descartada. Quem chamar a porta não teria como distinguir
   "legado decidiu que não" de "legado não respondeu", e o ticket é explícito: essa
   distinção decide se um Pedido é rejeitado ou continua elegível a reprocessamento.
2. **`ResultadoLegado` (Aprovado/Recusado) para toda resposta, incluindo
   indisponibilidade codificada como um terceiro caso do union** — mais uniforme, mas
   obrigaria todo chamador a tratar indisponibilidade como um valor de retorno comum,
   quando na prática é uma condição excepcional (falha de infraestrutura) que deveria
   interromper o fluxo normal, não ser um `switch` a mais.
3. **`ResultadoLegado` para decisão de negócio + `LegadoIndisponivelException` para
   falha técnica** — escolhida. `ILegadoPedidoGateway.ValidarEReservarAsync` retorna
   `ResultadoLegado.Aprovado` ou `ResultadoLegado.Recusado(motivo)` só quando o legado
   efetivamente respondeu com uma decisão; falha técnica (SOAP fault, timeout, circuito
   aberto) nunca vira um desses dois — sempre `LegadoIndisponivelException`. Dois tipos
   de propósito diferente, para que a ZER-183 nunca confunda os dois casos por engano
   de pattern matching incompleto.

## Opções consideradas — política de resiliência (Polly)

1. **Só retry** — insuficiente sozinho: contra um legado genuinamente fora do ar, retry
   sem circuit breaker apenas atrasa cada chamada pelo tempo total de todas as
   tentativas, sem parar de tentar uma rota que já se provou ruim.
2. **Retry (externo) envolvendo circuit breaker (interno), timeout por tentativa mais
   interno ainda** — escolhida, na ordem `AddRetry().AddCircuitBreaker().AddTimeout()`.
   Cada tentativa de retry passa pelo circuit breaker individualmente, então o circuito
   acumula estatística de falha por tentativa (não por chamada lógica); uma vez aberto,
   tentativas subsequentes (da mesma chamada ou de chamadas seguintes) recebem
   `BrokenCircuitException` na hora, sem re-tentar a rede. Parâmetros de produção:
   3 tentativas de retry, backoff exponencial com jitter a partir de 200ms, circuito
   abre a 50% de falha com no mínimo 4 amostras numa janela de 10s, fica aberto por
   15s, timeout de 2s por tentativa. Extraídos para `LegadoResiliencePipelineOptions`
   (não fixos no código) para que os testes de resiliência usem janelas de
   milissegundos sem depender dos valores de produção.

## Decisão

SoapCore hospeda o legado fake (`tools/LegadoErp.Fake`), subido via `docker compose up`
igual a qualquer outra infraestrutura local. `dotnet-svcutil` gera o cliente contra o
WSDL desse fake; o gerado fica em `OrderIntake.Infrastructure/Legado/Gerado`, nunca
editado à mão. `ILegadoPedidoGateway` (porta em `Application.Pedidos`) e
`ResultadoLegado`/`LegadoIndisponivelException` isolam completamente o resto do hub de
qualquer tipo de `System.ServiceModel`. `LegadoPedidoGateway` (adapter em
`Infrastructure.Legado`) envolve o cliente gerado num pipeline Polly
(`LegadoResiliencePipelineOptions`) e traduz toda falha técnica — fault SOAP, timeout,
circuito aberto — em `LegadoIndisponivelException`, nunca em `ResultadoLegado.Recusado`.

Registrado em `AddInfrastructure`, mas **não chamado por nenhum consumer** — a
`PedidoRecebidoConsumer` (ZER-161) continua só fazendo `Recebido → Validando`. Conectar
o gateway à saga, e decidir o que fazer quando `LegadoIndisponivelException` for
lançada (manter em `Recebido` para reprocessamento, não rejeitar — a instrução do
ticket ZER-162), é escopo da ZER-183.

## Consequências

- `System.ServiceModel` nunca atravessa `Application` — verificável olhando as
  `ProjectReference` de `OrderIntake.Application` (nenhuma delas é `Infrastructure`) e
  a assinatura pública de `ILegadoPedidoGateway`.
- Circuito aberto é observável sem heurística de tempo: `LegadoPedidoGateway.CircuitState`
  expõe o `CircuitBreakerStateProvider` do Polly diretamente, usado em
  `LegadoPedidoGatewayResilienceTests` para provar abertura/fechamento de forma
  determinística.
- **Negativa, aceita:** cada chamada cria e descarta um `ServicoLegadoClient` novo
  (`Abort()`, não `CloseAsync()` — `BasicHttpBinding` não tem sessão, e negociar um
  close gracioso a cada tentativa arriscava estourar o timeout do Polly sem ganho
  real). Mais caro que reusar um client de longa duração, mas evita de vez o problema
  clássico de canal WCF "faulted" sendo reusado depois de uma falha.
- **Negativa, aceita:** o gateway existe e é testado isoladamente, mas não demonstra
  nada ponta a ponta até a ZER-183 conectá-lo à saga. Decisão deliberada — ver Contexto.
