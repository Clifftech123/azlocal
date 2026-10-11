namespace AzLocal.Core.Models;

public class ServiceBusMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string? AmqpData { get; set; }
    public long SequenceNumber { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; }
    // Increments each time the message is received but not completed
    public int DeliveryCount { get; set; }
    public string? LockToken { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? DeadLetterReason { get; set; }
    public Dictionary<string, string> ApplicationProperties { get; set; } = new();
}
