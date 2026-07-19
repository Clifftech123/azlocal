using AzLocal.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AzLocal.IntegrationTests.Fixtures;

/// <summary>
/// Boots the AzLocal emulator (<c>AzLocal.Host</c>) in-process via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// — no separately started process, no real socket. State defaults to <c>InMemory</c> (the
/// host's own default), and blob file storage is redirected to a per-instance temp directory
/// so tests never touch <c>%TEMP%/azlocal</c> or collide with each other.
///
/// Exposes <see cref="Clients"/>, an <see cref="AzlocalClientFactory"/> whose Azure SDK clients
/// route through the in-memory <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>.
/// </summary>
public sealed class EmulatorFixture : WebApplicationFactory<Program>
{
    private readonly string _blobStorePath =
        Path.Combine(Path.GetTempPath(), "azlocal-inttest-blobs", Guid.NewGuid().ToString());

    public AzlocalClientFactory Clients { get; }

    public EmulatorFixture()
    {
        // Must be an IP literal, not "localhost" — see the comment on BlobRoutes.
        Clients = new AzlocalClientFactory("https://127.0.0.1", Server.CreateHandler());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzLocal:BlobStorePath"] = _blobStorePath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_blobStorePath))
            Directory.Delete(_blobStorePath, recursive: true);
    }
}
