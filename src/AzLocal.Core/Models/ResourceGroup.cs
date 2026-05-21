namespace AzLocal.Core.Models;

public class ResourceGroup : AzureResource
{
    // "Succeeded" means fully created and healthy
    public string ProvisioningState { get; set; } = "Succeeded";
    public string SubscriptionId { get; set; } = string.Empty;
}
