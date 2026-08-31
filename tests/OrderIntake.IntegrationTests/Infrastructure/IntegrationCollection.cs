using OrderIntake.IntegrationTests.Legado;

namespace OrderIntake.IntegrationTests.Infrastructure;

/// <summary>
/// SQL Server + RabbitMQ — só para testes que sobem o host completo via
/// OrderIntakeApiFactory (MassTransit tenta conectar no broker ao iniciar). Testes que
/// só precisam de SQL Server usam SqlServerCollection, mais barata.
///
/// LegadoFakeHostFixture (Kestrel in-process) também é compartilhado aqui, não
/// IClassFixture por classe: cada host Kestrel a mais é RAM/CPU a mais competindo com o
/// SQL Server + RabbitMQ desta collection — descoberto quando dois hosts simultâneos (um
/// por classe) derrubaram o próprio container do RabbitMQ sob pressão de memória
/// (BrokerUnreachableException, não timeout de aplicação).
/// </summary>
[CollectionDefinition(nameof(IntegrationCollection))]
public sealed class IntegrationCollection
    : ICollectionFixture<SqlServerContainerFixture>, ICollectionFixture<RabbitMqContainerFixture>, ICollectionFixture<LegadoFakeHostFixture>;
