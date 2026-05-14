namespace AzLocal.Core.Models;

public class ServiceBusMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; }
}
