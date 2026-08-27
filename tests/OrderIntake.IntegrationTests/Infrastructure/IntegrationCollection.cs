namespace OrderIntake.IntegrationTests.Infrastructure;

/// <summary>
/// SQL Server + RabbitMQ — só para testes que sobem o host completo via
/// OrderIntakeApiFactory (MassTransit tenta conectar no broker ao iniciar). Testes que
/// só precisam de SQL Server usam SqlServerCollection, mais barata.
/// </summary>
[CollectionDefinition(nameof(IntegrationCollection))]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerContainerFixture>, ICollectionFixture<RabbitMqContainerFixture>;
