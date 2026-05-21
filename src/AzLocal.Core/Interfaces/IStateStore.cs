namespace AzLocal.Core.Interfaces;

/// <summary>
/// Generic key-value store for all structured Azure resource data
/// (secrets, blob metadata, queues, resource groups, etc.).
///
/// Keys follow the pattern: "{service}/{resourceType}/{account}/{name}" — always lowercase.
/// Examples:
///   "keyvault/secrets/myvault/mysecret/abc123"
///   "blob/containers/myaccount/mycontainer"
///   "blob/items/myaccount/mycontainer/photo.jpg"
///   "arm/resourcegroups/sub-id/my-rg"
///
/// Three implementations — swap via AzLocal:StateMode in appsettings.json:
///   "InMemory"     → InMemoryStateStore     (default, fastest, data lost on restart)
///   "JsonSnapshot" → JsonSnapshotStateStore (persists to %TEMP%/azlocal/state.json)
///   "Sqlite"       → SqliteStateStore       (persists to %TEMP%/azlocal/state.db)
///
/// Note: Binary blob bytes are handled separately by IBlobFileStore, not this interface.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Retrieves the value stored at <paramref name="key"/>.
    /// Returns <c>null</c> if the key does not exist — never throws.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Stores <paramref name="value"/> at <paramref name="key"/>.
    /// Overwrites the existing value if the key already exists.
    /// </summary>
    Task SetAsync<T>(string key, T value);

    /// <summary>
    /// Removes the entry at <paramref name="key"/>.
    /// Does nothing if the key does not exist — never throws.
    /// </summary>
    Task DeleteAsync(string key);

    /// <summary>
    /// Returns all values whose key starts with <paramref name="prefix"/>.
    /// Returns an empty list if no keys match — never throws.
    /// Example: prefix "blob/containers/myaccount/" returns all containers for that account.
    /// </summary>
    Task<IReadOnlyList<T>> ListAsync<T>(string prefix);

    /// <summary>
    /// Returns <c>true</c> if a value exists at <paramref name="key"/>, <c>false</c> otherwise.
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
