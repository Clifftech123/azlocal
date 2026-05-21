using AzLocal.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AzLocal.State;

/// <summary>
/// File-system implementation of <see cref="IBlobFileStore"/>. Blobs are stored as plain files
/// under <c>%TEMP%/azlocal/blobs/{container}/{blobName}</c> by default, or a custom path via
/// <c>AzLocal:BlobStorePath</c> in configuration.
/// </summary>
public class TempFileBlobStore : IBlobFileStore
{
    private readonly string _basePath;

    public TempFileBlobStore(IConfiguration config)
    {
        _basePath = config["AzLocal:BlobStorePath"]
            ?? Path.Combine(Path.GetTempPath(), "azlocal", "blobs");
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>Writes <paramref name="content"/> to disk as a blob file.</summary>
    public async Task WriteAsync(string containerName, string blobName, Stream content, string contentType)
    {
        var path = BlobPath(containerName, blobName);
        // Create intermediate directories for blob names that contain slashes (e.g. "folder/file.txt").
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file);
    }

    /// <summary>Opens the blob file for reading. Throws <see cref="FileNotFoundException"/> if it does not exist.</summary>
    public Task<Stream> ReadAsync(string containerName, string blobName)
    {
        var path = BlobPath(containerName, blobName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Blob not found: {containerName}/{blobName}");
        // OpenRead returns a handle synchronously; actual reading is done by the caller.
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    /// <summary>Deletes the entire container directory and all blobs inside it. No-ops if the container does not exist.</summary>
    public Task DeleteContainerAsync(string containerName)
    {
        var path = Path.Combine(_basePath, containerName);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Returns true if the blob file exists on disk.</summary>
    public Task<bool> ExistsAsync(string containerName, string blobName)
    {
        return Task.FromResult(File.Exists(BlobPath(containerName, blobName)));
    }

    /// <summary>Deletes the blob file. No-ops if the blob does not exist.</summary>
    public Task DeleteAsync(string containerName, string blobName)
    {
        var path = BlobPath(containerName, blobName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    #region Private helpers

    private string BlobPath(string container, string blob) =>
        Path.Combine(_basePath, container, blob);

    #endregion
}
