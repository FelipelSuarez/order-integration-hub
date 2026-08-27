namespace OrderIntake.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(IntegrationCollection))]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerContainerFixture>, ICollectionFixture<RabbitMqContainerFixture>;
