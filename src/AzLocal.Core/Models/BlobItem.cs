namespace AzLocal.Core.Models;

public class BlobItem : AzureResource
{
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}
