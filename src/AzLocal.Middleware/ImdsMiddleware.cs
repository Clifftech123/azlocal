using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AzLocal.Middleware;

public class ImdsMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string FakeTokenJson = JsonSerializer.Serialize(new
    {
        access_token = "fake-azlocal-token",
        expires_on = "99999999999",
        resource = "https://management.azure.com/",
        token_type = "Bearer"
    });

    public ImdsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/metadata/identity/oauth2/token"))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(FakeTokenJson);
            return; // Do NOT call _next — response is already written
        }
        await _next(context);
    }
}


