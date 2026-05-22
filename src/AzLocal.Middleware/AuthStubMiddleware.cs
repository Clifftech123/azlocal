using AzLocal.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace AzLocal.Middleware;

/// <summary>
/// Bypasses Azure AD authentication for local development.
/// Injects a fake authenticated identity on every request so [Authorize] checks pass
/// without needing a real Azure AD token or tenant.
/// Identity values are read from <c>AzLocal:ObjectId</c> and <c>AzLocal:TenantId</c> in configuration.
/// </summary>
public class AuthStubMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ClaimsPrincipal _fakeUser;

    public AuthStubMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        var objectId = config["AzLocal:ObjectId"] ?? EmulatorDefaults.ObjectId;
        var tenantId = config["AzLocal:TenantId"] ?? EmulatorDefaults.TenantId;

        // authenticationType must be non-null/non-empty for IsAuthenticated to return true.
        _fakeUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", objectId),
            new Claim("tid", tenantId),
            new Claim(ClaimTypes.Name, "azlocal-dev"),
        ], authenticationType: "AzLocalStub"));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip real token validation — stamp every request as authenticated locally.
        context.User = _fakeUser;
        await _next(context);
    }
}
