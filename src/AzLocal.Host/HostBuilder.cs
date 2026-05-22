using AzLocal.Core.Interfaces;
using AzLocal.Middleware;
using AzLocal.Services.Arm;
using AzLocal.Services.BlobStorage;
using AzLocal.Services.KeyVault;
using AzLocal.Services.ServiceBus;
using AzLocal.State;

namespace AzLocal.Host;

/// <summary>
/// Configures all DI registrations for the AzLocal emulator.
/// Call <see cref="Configure"/> once in Program.cs before building the app.
/// </summary>
public static class EmulatorBuilder
{
    /// <summary>
    /// Registers all emulator services into the DI container:
    /// <list type="bullet">
    ///   <item>State store — selected by <c>AzLocal:StateMode</c> (InMemory / JsonSnapshot / Sqlite)</item>
    ///   <item>Blob file store</item>
    ///   <item>All <see cref="IServiceHandler"/> implementations (Blob, KeyVault, ServiceBus, ARM)</item>
    /// </list>
    /// </summary>
    public static WebApplicationBuilder Configure(WebApplicationBuilder builder)
    {
        RegisterStateStore(builder);

        builder.Services.AddSingleton<IBlobFileStore, TempFileBlobStore>();

        // Each IServiceHandler registers its own routes in MapRoutes().
        // Program.cs iterates all of them so adding a new service here is the only change needed.
        builder.Services.AddSingleton<IServiceHandler, BlobServiceHandler>();
        builder.Services.AddSingleton<IServiceHandler, KeyVaultSecretHandler>();
        builder.Services.AddSingleton<IServiceHandler, ServiceBusQueueHandler>();
        builder.Services.AddSingleton<IServiceHandler, ResourceGroupHandler>();
        builder.Services.AddSingleton<IServiceHandler, SubscriptionHandler>();

        builder.Services.AddAuthorization();

        return builder;
    }

    private static void RegisterStateStore(WebApplicationBuilder builder)
    {
        var mode = builder.Configuration["AzLocal:StateMode"] ?? "InMemory";
        switch (mode)
        {
            case "Sqlite":
                builder.Services.AddSingleton<IStateStore, SqliteStateStore>();
                break;
            case "JsonSnapshot":
                builder.Services.AddSingleton<IStateStore, JsonSnapshotStateStore>();
                break;
            default:
                builder.Services.AddSingleton<IStateStore, InMemoryStateStore>();
                break;
        }
    }
}
