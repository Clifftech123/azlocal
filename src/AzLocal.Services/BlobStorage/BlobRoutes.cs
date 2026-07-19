namespace AzLocal.Services.BlobStorage;

/// <summary>
/// Route constants for the Blob Storage emulator endpoints.
/// <c>{*blobName}</c> is a catch-all segment so blob names containing forward slashes
/// (e.g. "folder/subfolder/file.txt") are captured as a single parameter.
///
/// Deliberately unprefixed (unlike /arm, /kv, /sb): the Azure Storage SDK's BlobUriBuilder
/// only recognizes URIs shaped exactly "{host}/{account}/{container}/{blob}" (Azurite-style
/// path addressing) as path-style — any extra prefix segment breaks its internal parsing of
/// account/container names, which silently corrupts URIs built via BlobContainerClient.GetBlobClient()
/// and similar SDK convenience methods. The host must also be a literal IP (e.g. 127.0.0.1), not
/// "localhost" — see EmulatorDefaults.BaseUrl.
/// </summary>
public static class BlobRoutes
{
    public const string ListContainers = "/{account}";
    public const string Container = "/{account}/{container}";
    public const string BlobItem = "/{account}/{container}/{*blobName}";
}
