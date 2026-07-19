using AzLocal.Core;
using AzLocal.Middleware;
using AzLocal.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;

namespace AzLocal.UnitTests;

public class AuthStubMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_StampsAuthenticatedUser_AndCallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new AuthStubMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.True(context.User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_UsesConfiguredObjectIdAndTenantId()
    {
        var config = new InMemoryConfig(new Dictionary<string, string?>
        {
            ["AzLocal:ObjectId"] = "my-object-id",
            ["AzLocal:TenantId"] = "my-tenant-id"
        });
        var middleware = new AuthStubMiddleware(_ => Task.CompletedTask, config);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal("my-object-id", context.User.FindFirst("oid")!.Value);
        Assert.Equal("my-tenant-id", context.User.FindFirst("tid")!.Value);
    }

    [Fact]
    public async Task InvokeAsync_WithoutConfiguredIds_UsesDefaults()
    {
        var middleware = new AuthStubMiddleware(_ => Task.CompletedTask, new InMemoryConfig());
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(EmulatorDefaults.ObjectId, context.User.FindFirst("oid")!.Value);
        Assert.Equal(EmulatorDefaults.TenantId, context.User.FindFirst("tid")!.Value);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatesEvenWithGarbageAuthorizationHeader()
    {
        // The whole point of this middleware is to bypass real token validation locally —
        // an invalid/garbage bearer token must not cause a rejection.
        var middleware = new AuthStubMiddleware(_ => Task.CompletedTask, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer not-a-real-token";

        await middleware.InvokeAsync(context);

        Assert.True(context.User.Identity!.IsAuthenticated);
    }
}
