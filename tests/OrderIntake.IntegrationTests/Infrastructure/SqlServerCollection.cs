namespace OrderIntake.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>;
