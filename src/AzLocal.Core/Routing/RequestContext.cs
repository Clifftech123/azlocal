namespace AzLocal.Core.Routing;

public class RequestContext
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
}
