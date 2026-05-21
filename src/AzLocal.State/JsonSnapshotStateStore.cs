using AzLocal.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AzLocal.State;

/// <summary>
/// Same in-memory dictionary as <see cref="InMemoryStateStore"/>, but every write is also
/// persisted to a JSON file on disk. State survives process restarts.
/// Snapshot file defaults to <c>%TEMP%/azlocal/state.json</c> unless overridden via
/// <c>AzLocal:SnapshotPath</c> in configuration.
/// </summary>
public class JsonSnapshotStateStore : IStateStore
{
    private readonly string _filePath;

    // Limits concurrent file writes to one at a time so the snapshot is never half-written.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _store;

    public JsonSnapshotStateStore(IConfiguration config)
    {
        var dir = config["AzLocal:SnapshotPath"]
            ?? Path.Combine(Path.GetTempPath(), "azlocal");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "state.json");
        _store = Load();
    }

    /// <summary>Returns the entry deserialized as <typeparamref name="T"/>, or null if the key does not exist.</summary>
    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var json))
            return Task.FromResult(JsonSerializer.Deserialize<T>(json));
        return Task.FromResult<T?>(default);
    }

    /// <summary>Serializes <paramref name="value"/> to JSON, stores it in memory, and flushes the snapshot to disk.</summary>
    public async Task SetAsync<T>(string key, T value)
    {
        _store[key] = JsonSerializer.Serialize(value);
        await SaveAsync();
    }

    /// <summary>Removes the entry with the given key and flushes the snapshot to disk. No-ops if the key does not exist.</summary>
    public async Task DeleteAsync(string key)
    {
        _store.TryRemove(key, out _);
        await SaveAsync();
    }

    /// <summary>Returns all entries whose keys start with <paramref name="prefix"/>, deserialized as <typeparamref name="T"/>.</summary>
    public Task<IReadOnlyList<T>> ListAsync<T>(string prefix)
    {
        var results = _store
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => JsonSerializer.Deserialize<T>(kv.Value)!)
            .ToList();
        return Task.FromResult<IReadOnlyList<T>>(results);
    }

    /// <summary>Returns true if an entry with the given key exists.</summary>
    public Task<bool> ExistsAsync(string key) =>
        Task.FromResult(_store.ContainsKey(key));

    #region private helpers

    // Reads the snapshot file on startup. Returns an empty dictionary if the file does not exist yet.
    private ConcurrentDictionary<string, string> Load()
    {
        if (!File.Exists(_filePath))
            return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var json = File.ReadAllText(_filePath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        return new ConcurrentDictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
    }

    // Writes the full dictionary to disk under the semaphore so concurrent writes don't race.
    private async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(_filePath,
                JsonSerializer.Serialize(new Dictionary<string, string>(_store)));
        }
        finally { _lock.Release(); }
    }

    #endregion
}
