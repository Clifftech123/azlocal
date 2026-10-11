namespace AzLocal.Services.ServiceBus;

/// <summary>Serializes HTTP and AMQP queue operations within one host.</summary>
public sealed class ServiceBusCoordinator
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
