using AzLocal.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AzLocal.Middleware;

/// <summary>
/// Intercepts Azure IMDS (Instance Metadata Service) token requests so the Azure SDK
/// can acquire a credential locally without a real Azure VM or managed identity.
/// Token value is read from <c>AzLocal:FakeToken</c> in configuration.
/// </summary>
public class ImdsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _fakeTokenJson;

    public ImdsMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        var token = config["AzLocal:FakeToken"] ?? EmulatorDefaults.FakeToken;
        _fakeTokenJson = JsonSerializer.Serialize(new
        {
            access_token = token,
            expires_on   = "99999999999",
            resource     = "https://management.azure.com/",
            token_type   = "Bearer"
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/metadata/identity/oauth2/token"))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = 200;
            await context.Response.WriteAsync(_fakeTokenJson);
            return; // Do NOT call _next — response is already written.
        }
        await _next(context);
    }
}
