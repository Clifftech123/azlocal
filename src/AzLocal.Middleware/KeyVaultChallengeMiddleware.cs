using AzLocal.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AzLocal.Middleware;

/// <summary>
/// The Azure Key Vault SDK's ChallengeBasedAuthenticationPolicy always sends an initial,
/// anonymous, bodyless probe request expecting a 401 with a WWW-Authenticate challenge
/// before it will acquire a token and retry with the real request body attached — unlike
/// the Storage/Blob SDK, which attaches a bearer token optimistically on the first request.
///
/// AzLocal's AuthStubMiddleware accepts every request as authenticated regardless of headers,
/// so without this middleware the SDK's bodyless probe is what reaches the Key Vault handler
/// (never the real request), and every Set/Get Secret call fails.
/// </summary>
public class KeyVaultChallengeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _tenantId;

    public KeyVaultChallengeMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _tenantId = config["AzLocal:TenantId"] ?? EmulatorDefaults.TenantId;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/kv") && !context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.Append("WWW-Authenticate",
                $"Bearer authorization=\"https://login.microsoftonline.com/{_tenantId}\", resource=\"https://vault.azure.net\"");
            return; // Do NOT call _next — the SDK retries with a token once it sees this challenge.
        }

        await _next(context);
    }
}
