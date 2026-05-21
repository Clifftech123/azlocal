namespace AzLocal.Core.Models;

public class ServiceBusMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; }
    // Increments each time the message is received but not completed
    public int DeliveryCount { get; set; }
    public string? DeadLetterReason { get; set; }
    public Dictionary<string, string> ApplicationProperties { get; set; } = new();
}
