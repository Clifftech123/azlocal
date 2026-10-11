using AzLocal.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;

namespace AzLocal.CompatTests;

public sealed class CompatFixture : WebApplicationFactory<Program>
{
    private readonly string _blobs = Path.Combine(Path.GetTempPath(), "azlocal-compat", Guid.NewGuid().ToString("N"));
    public int AmqpPort { get; }
    public AzlocalClientFactory Clients => new("https://127.0.0.1", Server.CreateHandler());
    public CompatFixture()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        AmqpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzLocal:AmqpPort"] = AmqpPort.ToString(),
            ["AzLocal:BlobStorePath"] = _blobs
        }));
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_blobs)) Directory.Delete(_blobs, true);
    }
}
