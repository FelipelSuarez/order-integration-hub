# ADR-0005 — Transporte: RabbitMQ local, Azure Service Bus em produção

## Contexto

O outbox (ADR-0003) decide *que* a publicação é transacional; falta decidir *qual*
broker recebe as mensagens entregues pelo `BusOutboxDeliveryService`, tanto em
desenvolvimento/CI quanto em produção.

## Opções consideradas

1. **RabbitMQ em todo lugar, inclusive produção** — mais simples de manter (um único
   transporte, um único código de configuração), e é o que já roda localmente via
   Testcontainers. Mas não demonstra nada sobre operar contra um serviço gerenciado de
   nuvem, que é a realidade mais comum de produção .NET em Azure — e é justamente essa
   familiaridade que o projeto quer provar.
2. **Azure Service Bus em todo lugar, inclusive desenvolvimento local** — realista para
   produção, mas exige assinatura Azure e credencial de nuvem só para rodar
   `dotnet test` ou subir a API localmente. Trava onboarding e CI atrás de um recurso
   pago e externo, para um projeto de portfólio que precisa rodar em qualquer máquina
   com Docker.
3. **MassTransit configurado com os dois transportes, selecionado por configuração** —
   escolhida. A mesma abstração (`IPublishEndpoint`, `IConsumer<T>`) funciona contra
   qualquer um dos dois; só a configuração de host (`UsingRabbitMq` / `UsingAzureServiceBus`)
   muda.

## Decisão

`Messaging:Transport` em configuração (`RabbitMq` por padrão, `AzureServiceBus` como
alternativa) decide, em `AddMassTransit`, qual `UsingXxx` é registrado. As connection
strings (`ConnectionStrings:RabbitMq`, `ConnectionStrings:AzureServiceBus`) vêm de
`dotnet user-secrets` em desenvolvimento e de variável de ambiente no compose — mesmo
padrão já usado para `ConnectionStrings:OrderIntakeDb`; só o nome do transporte ativo
fica em `appsettings.json`, por não ser segredo. Local e CI usam RabbitMQ (compose e
Testcontainers); produção usaria Azure Service Bus.

## Consequências

- Trocar de transporte é mudança de configuração, não de código — o objetivo prático da
  abstração.
- **Negativa, aceita:** só o caminho RabbitMQ é exercitado por teste automatizado
  (Testcontainers). O caminho Azure Service Bus não tem verificação automatizada
  enquanto não existir um ambiente Azure real para apontar — risco aceito
  conscientemente, não descoberto em produção.
- Dois pacotes de transporte (`MassTransit.RabbitMQ`, `MassTransit.Azure.ServiceBus.Core`)
  na árvore de dependências mesmo que só um esteja ativo em cada ambiente.
