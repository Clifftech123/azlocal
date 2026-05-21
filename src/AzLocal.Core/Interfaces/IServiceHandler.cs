using Microsoft.AspNetCore.Builder;

namespace AzLocal.Core.Interfaces;

/// <summary>
/// Plug-in contract for every emulated Azure service (Blob, KeyVault, ServiceBus, ARM, etc.).
///
/// How it works:
///   1. Each service implements this interface and registers its own HTTP routes in MapRoutes().
///   2. HostBuilder registers each implementation as IServiceHandler in DI.
///   3. Program.cs loops over all IServiceHandler instances and calls MapRoutes() on each —
///      so adding a new service never requires changing Program.cs.
///
/// To add a new service:
///   - Create a class that implements IServiceHandler
///   - Register it in HostBuilder.cs with: builder.Services.AddSingleton&lt;IServiceHandler, YourHandler&gt;()
///   - Done — the host picks it up automatically on next startup
/// </summary>
public interface IServiceHandler
{
    /// <summary>
    /// Human-readable name used in log output — e.g. "BlobStorage", "KeyVault".
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Called once at startup. Register all HTTP routes this service handles here.
    /// Use app.MapGet / MapPut / MapPost / MapDelete to register route handlers.
    /// </summary>
    void MapRoutes(WebApplication app);
}
