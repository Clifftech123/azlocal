using AzLocal.Core.Interfaces;
using AzLocal.Host;
using AzLocal.Middleware;

var builder = WebApplication.CreateBuilder(args);
EmulatorBuilder.Configure(builder);

var app = builder.Build();

// Middleware pipeline — order matters.
app.UseMiddleware<RequestLoggingMiddleware>();  // 1. log every request
app.UseMiddleware<ImdsMiddleware>();            // 2. intercept IMDS token requests
app.UseMiddleware<AuthStubMiddleware>();        // 3. stamp all requests as authenticated
app.UseAuthorization();                        // 4. enforce [Authorize] using the stub identity
app.UseMiddleware<RequestContextMiddleware>(); // 5. parse and store Azure request context

// Auto-discover and register every IServiceHandler's routes.
// Adding a new service only requires registering it in HostBuilder — nothing changes here.
foreach (var handler in app.Services.GetServices<IServiceHandler>())
{
    handler.MapRoutes(app);
    app.Logger.LogInformation("Registered service: {ServiceName}", handler.ServiceName);
}

var baseUrl = app.Configuration["AzLocal:BaseUrl"] ?? "http://localhost:4566";
app.MapGet("/", () => new
{
    status  = "AzLocal emulator running",
    version = "1.0",
    docs    = $"{baseUrl}/"
});

app.Run();
