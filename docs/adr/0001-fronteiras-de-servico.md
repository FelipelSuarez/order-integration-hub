# ADR-0001 — Fronteiras de serviço

## Contexto

O hub integra pedidos entre uma API moderna e um ERP legado via SOAP, com
mensageria e projeção de leitura separada. É preciso decidir quantos serviços
compõem o sistema antes de desenhar o resto.

## Opções consideradas

1. **Monólito único** — recebe, valida contra o legado, persiste e expõe
   consulta no mesmo processo/banco. Mais simples de operar, mas mistura
   escrita e leitura no mesmo modelo e não demonstra mensageria real nem
   fronteira de consistência entre processos — para portfólio, prova o mesmo
   que uma API CRUD.
2. **Microsserviços por bounded context "canônico"** (Pedido, Cliente,
   Estoque, Pagamento, Notificação, Gateway...) — mostra familiaridade com o
   vocabulário de microsserviços, mas o overhead operacional (múltiplos
   bancos, pipelines, configs) não é justificado pelo domínio real e distrai
   do que o projeto quer provar: decisão de consistência e concorrência, não
   quantidade de infraestrutura.
3. **Dois serviços — `OrderIntake` e `OrderProjection`** — escolhido.

## Decisão

Dois serviços. `OrderIntake` recebe pedidos, valida contra o legado, persiste
transacionalmente e publica eventos. `OrderProjection` consome esses eventos e
mantém um read model desnormalizado. Comunicação entre os dois é assíncrona,
via broker. Nenhum terceiro serviço entra a menos que demonstre algo que os
dois já não demonstram — falha, concorrência ou estado novo.

## Consequências

- Mensageria e consistência eventual reais para observar, não simuladas.
- Separação clara entre escrita e leitura.
- **Negativa, aceita:** complexidade operacional (dois bancos, um broker,
  dois processos, testcontainers mais pesados) maior que o necessário para o
  volume de tráfego atual — que é zero em produção. O custo é pago
  deliberadamente pelo valor pedagógico, não por necessidade de escala.
