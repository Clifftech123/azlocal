using AzLocal.Middleware;
using AzLocal.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AzLocal.UnitTests;

public class ImdsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ForImdsPath_ReturnsTokenJson_WithoutCallingNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new ImdsMiddleware(next, new InMemoryConfig("AzLocal:FakeToken", "my-fake-token"));
        var context = new DefaultHttpContext();
        context.Request.Path = "/metadata/identity/oauth2/token";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = JsonDocument.Parse(context.Response.Body);
        Assert.Equal("my-fake-token", doc.RootElement.GetProperty("access_token").GetString());
        Assert.Equal("Bearer", doc.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ForImdsPath_WithoutConfiguredToken_UsesDefaultFakeToken()
    {
        var middleware = new ImdsMiddleware(_ => Task.CompletedTask, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/metadata/identity/oauth2/token";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = JsonDocument.Parse(context.Response.Body);
        Assert.Equal(AzLocal.Core.EmulatorDefaults.FakeToken, doc.RootElement.GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ForImdsPath_MatchesSubPaths()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new ImdsMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/metadata/identity/oauth2/token/extra";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ForOtherPaths_CallsNext_WithoutWritingResponse()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new ImdsMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/azu/myaccount/mycontainer";

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(200, context.Response.StatusCode); // default, untouched
    }
}
