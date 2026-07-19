using AzLocal.Middleware;
using AzLocal.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;

namespace AzLocal.UnitTests;

public class KeyVaultChallengeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ForKvPath_WithoutAuthorizationHeader_Returns401WithChallenge()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new KeyVaultChallengeMiddleware(next, new InMemoryConfig("AzLocal:TenantId", "my-tenant"));
        var context = new DefaultHttpContext();
        context.Request.Path = "/kv/myvault/secrets/mysecret";

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(401, context.Response.StatusCode);
        var challenge = context.Response.Headers["WWW-Authenticate"].ToString();
        Assert.Contains("Bearer", challenge);
        Assert.Contains("https://login.microsoftonline.com/my-tenant", challenge);
        Assert.Contains("resource=\"https://vault.azure.net\"", challenge);
    }

    [Fact]
    public async Task InvokeAsync_ForKvPath_WithAuthorizationHeader_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new KeyVaultChallengeMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/kv/myvault/secrets/mysecret";
        context.Request.Headers.Authorization = "Bearer some-token";

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(200, context.Response.StatusCode); // default, untouched
    }

    [Fact]
    public async Task InvokeAsync_ForNonKvPath_CallsNext_EvenWithoutAuthorizationHeader()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new KeyVaultChallengeMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/myaccount/mycontainer/myblob.txt";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_WithoutConfiguredTenantId_UsesDefault()
    {
        var middleware = new KeyVaultChallengeMiddleware(_ => Task.CompletedTask, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/kv/myvault/secrets/mysecret";

        await middleware.InvokeAsync(context);

        var challenge = context.Response.Headers["WWW-Authenticate"].ToString();
        Assert.Contains(AzLocal.Core.EmulatorDefaults.TenantId, challenge);
    }
}
