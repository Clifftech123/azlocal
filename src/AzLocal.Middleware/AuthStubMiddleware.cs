using Microsoft.AspNetCore.Http;

namespace AzLocal.Middleware;

public class AuthStubMiddleware
{
    private readonly RequestDelegate _next;

    public AuthStubMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context) => await _next(context);
}
