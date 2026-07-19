using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace AzLocal.UnitTests.TestHelpers;

/// <summary>
/// Minimal <see cref="IConfiguration"/> stub for tests. The state store constructors
/// only ever read a single key via the string indexer, so that's all this implements.
/// </summary>
internal sealed class InMemoryConfig : IConfiguration
{
    private readonly Dictionary<string, string?> _values;

    public InMemoryConfig(string key, string? value) =>
        _values = new Dictionary<string, string?> { [key] = value };

    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => throw new NotSupportedException();
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
}
