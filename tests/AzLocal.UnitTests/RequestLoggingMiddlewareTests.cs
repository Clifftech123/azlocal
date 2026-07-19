using AzLocal.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzLocal.UnitTests;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new RequestLoggingMiddleware(next, NullLogger<RequestLoggingMiddleware>.Instance);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_PropagatesException_FromNext()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new RequestLoggingMiddleware(next, NullLogger<RequestLoggingMiddleware>.Instance);
        var context = new DefaultHttpContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
