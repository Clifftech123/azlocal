namespace AzLocal.Core.Routing;

public class RequestContext
{ // From path: /subscriptions/{this}/...
    public string SubscriptionId { get; set; } = string.Empty;
    // From path: /resourceGroups/{this}/...
    public string ResourceGroup { get; set; } = string.Empty;
    // "blob", "keyvault", "servicebus", or "arm"
    public string ServiceType { get; set; } = string.Empty;
    // Storage account or vault name from the Host header subdomain
    public string AccountName { get; set; } = string.Empty;
    // The api-version query parameter value
    public string ApiVersion { get; set; } = string.Empty;
}
