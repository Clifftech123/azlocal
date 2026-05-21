using AzLocal.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AzLocal.State;

/// <summary>
/// SQLite-backed implementation of <see cref="IStateStore"/>. State is persisted to a
/// <c>state.db</c> file and survives process restarts.
/// Database file defaults to <c>%TEMP%/azlocal/state.db</c> unless overridden via
/// <c>AzLocal:SqlitePath</c> in configuration.
/// </summary>
public class SqliteStateStore : IStateStore
{
    private readonly string _connectionString;

    public SqliteStateStore(IConfiguration config)
    {
        var dir = config["AzLocal:SqlitePath"]
            ?? Path.Combine(Path.GetTempPath(), "azlocal");
        Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={Path.Combine(dir, "state.db")}";
        EnsureTable();
    }

    /// <summary>Returns the entry deserialized as <typeparamref name="T"/>, or null if the key does not exist.</summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM store WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key.ToLowerInvariant());
        var result = await cmd.ExecuteScalarAsync();
        return result is string json ? JsonSerializer.Deserialize<T>(json) : default;
    }

    /// <summary>Serializes <paramref name="value"/> to JSON and upserts it under <paramref name="key"/>.</summary>
    public async Task SetAsync<T>(string key, T value)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        // INSERT OR UPDATE in one atomic statement — no separate exists check needed.
        cmd.CommandText = @"INSERT INTO store (key, value) VALUES ($key, $value)
                            ON CONFLICT(key) DO UPDATE SET value = $value";
        cmd.Parameters.AddWithValue("$key", key.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$value", JsonSerializer.Serialize(value));
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Removes the entry with the given key. No-ops if the key does not exist.</summary>
    public async Task DeleteAsync(string key)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM store WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key.ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Returns all entries whose keys start with <paramref name="prefix"/>, deserialized as <typeparamref name="T"/>.</summary>
    public async Task<IReadOnlyList<T>> ListAsync<T>(string prefix)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        // LIKE with trailing % uses the PRIMARY KEY index — efficient prefix scan.
        cmd.CommandText = "SELECT value FROM store WHERE key LIKE $prefix";
        cmd.Parameters.AddWithValue("$prefix", prefix.ToLowerInvariant() + "%");
        var results = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = JsonSerializer.Deserialize<T>(reader.GetString(0));
            if (item is not null) results.Add(item);
        }
        return results;
    }

    /// <summary>Returns true if an entry with the given key exists.</summary>
    public async Task<bool> ExistsAsync(string key)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM store WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key.ToLowerInvariant());
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    #region Private helpers

    // Creates the store table on first run. Synchronous because it runs once at startup before any async calls.
    private void EnsureTable()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS store (key TEXT PRIMARY KEY, value TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
    }

    #endregion
}
