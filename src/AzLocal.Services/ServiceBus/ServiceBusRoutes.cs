namespace AzLocal.Services.ServiceBus;

/// <summary>
/// Route constants for the Service Bus queue emulator endpoints.
/// Peek-lock flow: POST head → receive with lock token → DELETE lock to complete, or POST deadletterqueue to dead-letter.
/// Destructive flow: DELETE head → receive and immediately remove in one step.
/// </summary>
public static class ServiceBusRoutes
{
    public const string Messages    = "/sb/{namespace}/{queue}/messages";
    public const string MessageHead = "/sb/{namespace}/{queue}/messages/head";
    public const string MessageLock = "/sb/{namespace}/{queue}/messages/{lockToken}";
    public const string DeadLetter  = "/sb/{namespace}/{queue}/messages/{lockToken}/deadletterqueue";
}
