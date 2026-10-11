using AzLocal.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AzLocal.Middleware;

/// <summary>Local managed identity token endpoint. Tokens are for emulator use only.</summary>
public class ImdsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _token;
    public ImdsMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _token = config["AzLocal:FakeToken"] ?? EmulatorDefaults.FakeToken;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(new PathString("/metadata/identity/getplatformmetadata")))
        {
            // This local endpoint supports bearer tokens, not IMDSv2 key-bound tokens.
            context.Response.StatusCode = 501;
            return;
        }
        if (context.Request.Path.Equals(new PathString("/metadata/instance/compute")))
        {
            await context.Response.WriteAsJsonAsync(new { osType = "Linux", securityProfile = new { securityType = "Standard" } });
            return;
        }
        if (!context.Request.Path.Equals(new PathString("/metadata/identity/oauth2/token")))
        {
            await _next(context);
            return;
        }
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = 405;
            context.Response.Headers.Allow = "GET";
            return;
        }
        var resource = context.Request.Query["resource"].ToString();
        if (!string.Equals(context.Request.Headers["Metadata"], "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(context.Request.Query["api-version"]) ||
            !Uri.TryCreate(resource, UriKind.Absolute, out _))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_request", error_description = "Metadata: true, api-version, and an absolute resource URI are required." });
            return;
        }
        var now = DateTimeOffset.UtcNow;
        await context.Response.WriteAsJsonAsync(new
        {
            access_token = _token,
            expires_on = now.AddHours(1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            not_before = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            expires_in = "3600",
            resource,
            token_type = "Bearer",
            client_id = context.Request.Query["client_id"].ToString()
        });
    }
}
