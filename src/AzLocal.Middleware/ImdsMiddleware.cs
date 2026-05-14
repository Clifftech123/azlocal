namespace AzLocal.Middleware;

public class ImdsMiddleware
{
    private readonly RequestDelegate _next;

    public ImdsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context) => await _next(context);
}
