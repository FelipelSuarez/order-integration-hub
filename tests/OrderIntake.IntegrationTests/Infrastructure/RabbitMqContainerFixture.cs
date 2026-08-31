using Testcontainers.RabbitMq;

namespace OrderIntake.IntegrationTests.Infrastructure;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    // rabbitmq:4-management + plugin rabbitmq_delayed_message_exchange pré-habilitado —
    // a saga (ZER-183) precisa dele pra reagendar retentativa quando o legado está
    // indisponível (UseDelayedMessageScheduler do MassTransit).
    private readonly RabbitMqContainer _container =
        new RabbitMqBuilder("heidiks/rabbitmq-delayed-message-exchange:4.2.0-management").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
