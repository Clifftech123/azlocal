namespace AzLocal.Client;

/// <summary>
/// xUnit test fixture that provides a pre-configured <see cref="AzlocalClientFactory"/>
/// pointed at the local AzLocal emulator. Use as a class fixture in integration tests:
/// <code>
/// public class MyTests : IClassFixture&lt;AzlocalFixture&gt;
/// {
///     public MyTests(AzlocalFixture fixture) { ... }
/// }
/// </code>
/// </summary>
public sealed class AzlocalFixture : IAsyncDisposable
{
    public AzlocalClientFactory Clients { get; }

    public AzlocalFixture(string baseUrl = "http://localhost:4566")
    {
        Clients = new AzlocalClientFactory(baseUrl);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
