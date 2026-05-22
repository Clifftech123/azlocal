namespace AzLocal.Services.BlobStorage;

public static class BlobRoutes
{
    public const string ListContainers = "/azu/{account}";
    public const string Container = "/azu/{account}/{container}";
    public const string BlobItem = "/azu/{account}/{container}/{*blobName}";
}
