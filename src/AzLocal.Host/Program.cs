using AzLocal.Core.Interfaces;
using AzLocal.Host;
using AzLocal.Middleware;

var builder = WebApplication.CreateBuilder(args);
EmulatorBuilder.Configure(builder);

var app = builder.Build();

// Middleware pipeline — order matters.
app.UseMiddleware<RequestLoggingMiddleware>();     // 1. log every request
app.UseMiddleware<ImdsMiddleware>();               // 2. intercept IMDS token requests
app.UseMiddleware<KeyVaultChallengeMiddleware>();  // 3. answer the Key Vault SDK's auth challenge probe
app.UseMiddleware<AuthStubMiddleware>();           // 4. stamp all requests as authenticated
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
    version = "1.0",
    docs    = $"{baseUrl}/"
});

app.Run();

// Exposes the top-level-statement Program class to WebApplicationFactory<Program>
// in AzLocal.IntegrationTests, which needs a public entry point to boot the host in-process.
public partial class Program { }
