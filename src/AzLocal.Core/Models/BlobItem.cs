namespace AzLocal.Core.Models;

public class BlobItem : AzureResource
{
public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    // Changes on every write — clients use this to detect staleness
    public string ETag { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    // BlockBlob is the standard type for general-purpose uploads
    public string BlobType { get; set; } = "BlockBlob";
    // Which container this blob lives in
    public string ContainerName { get; set; } = string.Empty;
}
