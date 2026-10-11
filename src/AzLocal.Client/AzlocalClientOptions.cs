using AzLocal.Core;

namespace AzLocal.Client;

/// <summary>Endpoints and resource names for local SDK client registration.</summary>
public sealed record AzlocalClientOptions
{
    public string BaseUrl { get; set; } = EmulatorDefaults.BaseUrl;
    public int AmqpPort { get; set; } = 5672;
    public string? BlobAccountName { get; set; }
    public string? BlobContainerName { get; set; }
    public string? KeyVaultName { get; set; }
    public string? ServiceBusQueueName { get; set; }
    public string? SubscriptionId { get; set; }

    /// <summary>Optional caller-owned transport for an in-process HTTP test host.</summary>
    public HttpMessageHandler? TransportHandler { get; set; }
}
