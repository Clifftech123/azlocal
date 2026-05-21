using AzLocal.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AzLocal.State;

/// <summary>
/// An in-memory implementation of <see cref="IStateStore"/> backed by a thread-safe dictionary.
/// Intended for local development and testing — state is lost when the process exits.
/// </summary>
public class InMemoryStateStore : IStateStore
{
    // Keys are case-insensitive so "Blob/Items/X" and "blob/items/x" resolve to the same entry.
    private readonly ConcurrentDictionary<string, string> _store =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Removes the entry with the given key. No-ops if the key does not exist.</summary>
    public Task DeleteAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>Returns true if an entry with the given key exists.</summary>
    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <summary>Serializes <paramref name="value"/> to JSON and stores it under <paramref name="key"/>.</summary>
    public Task SetAsync<T>(string key, T value)
    {
        _store[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
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

    /// <summary>Returns the entry deserialized as <typeparamref name="T"/>, or null if the key does not exist.</summary>
    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var json))
            return Task.FromResult(JsonSerializer.Deserialize<T>(json));

        return Task.FromResult<T?>(default);
    }
}
