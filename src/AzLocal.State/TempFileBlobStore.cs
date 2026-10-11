using AzLocal.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AzLocal.State;

/// <summary>
/// File-system implementation of <see cref="IBlobFileStore"/>. Blobs are stored as plain files
/// under <c>%TEMP%/azlocal/blobs/{encodedAccount}/{encodedContainer}/{encodedBlob}</c> by default, or a custom path via
/// <c>AzLocal:BlobStorePath</c> in configuration.
/// </summary>
public class TempFileBlobStore : IBlobFileStore
{
    private readonly string _basePath;

    public TempFileBlobStore(IConfiguration config)
    {
        _basePath = Path.GetFullPath((string.IsNullOrWhiteSpace(config["AzLocal:BlobStorePath"]) ? null : config["AzLocal:BlobStorePath"])
            ?? Path.Combine(Path.GetTempPath(), "azlocal", "blobs"));
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>Writes <paramref name="content"/> to disk as a blob file.</summary>
    public async Task WriteAsync(string account, string containerName, string blobName, Stream content, string contentType)
    {
        var path = BlobPath(account, containerName, blobName);
        // Each account and container has a separate directory.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file);
    }

    /// <summary>Opens the blob file for reading. Throws <see cref="FileNotFoundException"/> if it does not exist.</summary>
    public Task<Stream> ReadAsync(string account, string containerName, string blobName)
    {
        var path = BlobPath(account, containerName, blobName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Blob not found: {containerName}/{blobName}");
        // OpenRead returns a handle synchronously; actual reading is done by the caller.
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    /// <summary>Deletes the entire container directory and all blobs inside it. No-ops if the container does not exist.</summary>
    public Task DeleteContainerAsync(string account, string containerName)
    {
        var path = ContainerPath(account, containerName);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Returns true if the blob file exists on disk.</summary>
    public Task<bool> ExistsAsync(string account, string containerName, string blobName)
    {
        return Task.FromResult(File.Exists(BlobPath(account, containerName, blobName)));
    }

    /// <summary>Deletes the blob file. No-ops if the blob does not exist.</summary>
    public Task DeleteAsync(string account, string containerName, string blobName)
    {
        var path = BlobPath(account, containerName, blobName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    #region Private helpers

    // Names are data, never filesystem paths. Hashes preserve case on Windows.
    private static string Encode(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private string ContainerPath(string account, string container)
    {
        var path = Path.Combine(_basePath, Encode(account), Encode(container));
        RejectLinks(path);
        return path;
    }

    private string BlobPath(string account, string container, string blob)
    {
        var path = Path.Combine(ContainerPath(account, container), Encode(blob));
        RejectLinks(path);
        return path;
    }

    private void RejectLinks(string path)
    {
        // Inspect the configured root and its children. System ancestors outside
        // that root may legitimately be links, such as macOS temporary directories.
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current)) {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Blob storage paths must not contain symbolic links or junctions.");
            if (string.Equals(current, _basePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                break;
        }
    }

    #endregion
}
