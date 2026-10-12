using AzLocal.Core.Interfaces;
using AzLocal.Core;
using AzLocal.Middleware;

namespace AzLocal.Host;

public static class EmulatorHost
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length == 2 && args[0] == "--create-container-certificate")
        {
            ContainerCertificate.Create(args[1]);
            return;
        }
        if (args.Length == 1 && args[0] == "--healthcheck")
        {
            Environment.ExitCode = await ContainerCertificate.CheckHealthAsync() ? 0 : 1;
            return;
        }
        var builder = WebApplication.CreateBuilder(args);
        var containerNetworking = ContainerNetworkPolicy.IsEnabled(builder.Configuration);
        if (builder.Configuration["urls"] is { } urls)
            foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    !ContainerNetworkPolicy.AllowsListener(uri, containerNetworking))
                    throw new InvalidOperationException("AzLocal bypasses authentication and only supports loopback listeners.");
        EmulatorBuilder.Configure(builder);

        var app = builder.Build();

        // Middleware pipeline — order matters.
        app.UseMiddleware<RequestLoggingMiddleware>();     // 1. log every request
        app.UseMiddleware<AuthStubMiddleware>(); // Reject remote clients before token stubs.
        app.UseMiddleware<ImdsMiddleware>();               // 2. intercept IMDS token requests
        app.UseMiddleware<KeyVaultChallengeMiddleware>();  // 3. answer the Key Vault SDK's auth challenge probe
        app.UseAuthorization();                           // 5. enforce [Authorize] using the stub identity
        app.UseMiddleware<RequestContextMiddleware>();    // 6. parse and store Azure request context

        // Auto-discover and register every IServiceHandler's routes.
        // Adding a new service only requires registering it in HostBuilder — nothing changes here.
        foreach (var handler in app.Services.GetServices<IServiceHandler>())
        {
            handler.MapRoutes(app);
            app.Logger.LogInformation("Registered service: {ServiceName}", handler.ServiceName);
        }

        var baseUrl = app.Configuration["AzLocal:BaseUrl"] ?? "https://127.0.0.1:4566";
        app.MapGet("/", () => new
        {
            status  = "AzLocal emulator running",
            version = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(EmulatorHost).Assembly)?.InformationalVersion,
            docs    = $"{baseUrl}/"
        });

        await app.RunAsync();

    }
}
