using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AzLocal.Middleware;

/// <summary>
/// Bypasses Azure AD authentication for local development.
/// Injects a fake authenticated identity on every request so [Authorize] checks pass
/// without needing a real Azure AD token or tenant.
/// </summary>
public class AuthStubMiddleware
{
    private readonly RequestDelegate _next;

    // Static so the same fake identity is reused on every request — no per-request allocation.
    // authenticationType must be non-null/non-empty for IsAuthenticated to return true.
    private static readonly ClaimsPrincipal FakeUser = new(new ClaimsIdentity(
    [
        new Claim("oid", "00000000-0000-0000-0000-000000000001"),  // fake Azure object ID
        new Claim("tid", "00000000-0000-0000-0000-000000000002"),  // fake tenant ID
        new Claim(ClaimTypes.Name, "azlocal-dev"),
    ], authenticationType: "AzLocalStub"));

    public AuthStubMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip real token validation — stamp every request as authenticated locally.
        context.User = FakeUser;
        await _next(context);
    }
}
