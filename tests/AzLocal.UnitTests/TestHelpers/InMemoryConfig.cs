using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace AzLocal.UnitTests.TestHelpers;

/// <summary>
/// Minimal <see cref="IConfiguration"/> stub for tests. Constructors under test only ever
/// read config via the string indexer, so that's all this implements.
/// </summary>
internal sealed class InMemoryConfig : IConfiguration
{
    private readonly Dictionary<string, string?> _values;

    public InMemoryConfig() => _values = new Dictionary<string, string?>();

    public InMemoryConfig(string key, string? value) =>
        _values = new Dictionary<string, string?> { [key] = value };

    public InMemoryConfig(Dictionary<string, string?> values) => _values = values;

    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => throw new NotSupportedException();
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
}
