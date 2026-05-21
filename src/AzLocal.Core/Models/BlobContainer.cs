namespace AzLocal.Core.Models;

public class BlobContainer : AzureResource
{

  // "private" means no anonymous public read access — the default
    public string PublicAccessLevel { get; set; } = "private";
    public DateTimeOffset CreatedOn { get; set; } =  DateTimeOffset.UtcNow;
    // "available" means no active lease lock on this container
    public string LeaseState { get; set; } = "available";
}   

