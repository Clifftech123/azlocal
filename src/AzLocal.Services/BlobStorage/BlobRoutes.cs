namespace AzLocal.Services.BlobStorage;

/// <summary>
/// Route constants for the Blob Storage emulator endpoints.
/// <c>{*blobName}</c> is a catch-all segment so blob names containing forward slashes
/// (e.g. "folder/subfolder/file.txt") are captured as a single parameter.
/// </summary>
public static class BlobRoutes
{
    public const string ListContainers = "/azu/{account}";
    public const string Container = "/azu/{account}/{container}";
    public const string BlobItem = "/azu/{account}/{container}/{*blobName}";
}
