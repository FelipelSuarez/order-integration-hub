using Xunit;

// SqlServerCollection e IntegrationCollection sobem containers de SQL Server/RabbitMQ
// próprios cada uma; em paralelo (padrão do xUnit), competem por CPU/memória e derrubam
// o timeout de PedidoRecebidoMessagingTests numa máquina com poucos recursos. Sequencial
// custa tempo de execução, não corretude — troca aceita.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
