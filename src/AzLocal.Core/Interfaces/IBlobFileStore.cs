namespace AzLocal.Core.Interfaces;

/// <summary>
/// Stores and retrieves the raw binary bytes of blob files.
///
/// Why this is separate from IStateStore:
///   Blobs are arbitrary binary data (images, documents, videos, etc.) that can be very large.
///   They do not fit in a JSON key-value store. IStateStore holds blob metadata (name, size,
///   content type, ETag). This interface holds the actual bytes.
///
/// The only implementation is TempFileBlobStore, which writes files to disk under:
///   %TEMP%/azlocal/blobs/{containerName}/{blobName}
///
/// blobName can contain forward slashes (e.g. "folder/subfolder/file.txt") —
/// TempFileBlobStore creates the necessary subdirectories automatically.
/// </summary>
public interface IBlobFileStore
{
    /// <summary>
    /// Writes the contents of <paramref name="content"/> as a blob file to disk.
    /// Creates any intermediate directories if they do not exist.
    /// Overwrites the file if it already exists.
    /// </summary>
    Task WriteAsync(string containerName, string blobName, Stream content, string contentType);

    /// <summary>
    /// Opens the blob as a readable stream. The caller is responsible for disposing the stream.
    /// Throws <see cref="FileNotFoundException"/> if the blob does not exist.
    /// </summary>
    Task<Stream> ReadAsync(string containerName, string blobName);

    /// <summary>
    /// Deletes a single blob file.
    /// Does nothing if the blob does not exist — never throws.
    /// </summary>
    Task DeleteAsync(string containerName, string blobName);

    /// <summary>
    /// Deletes the entire container directory and all blob files inside it.
    /// Does nothing if the container does not exist — never throws.
    /// Called when a container is deleted via the Blob Storage API.
    /// </summary>
    Task DeleteContainerAsync(string containerName);

    /// <summary>
    /// Returns <c>true</c> if the blob file exists on disk, <c>false</c> otherwise.
    /// </summary>
    Task<bool> ExistsAsync(string containerName, string blobName);
}
