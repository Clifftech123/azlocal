namespace AzLocal.Middleware;

public class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context) => await _next(context);
}
