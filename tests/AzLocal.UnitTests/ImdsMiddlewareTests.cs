using AzLocal.Middleware;
using AzLocal.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AzLocal.UnitTests;

public class ImdsMiddlewareTests
{
    [Theory]
    [InlineData("GET", "false", "?api-version=2018-02-01&resource=https%3A%2F%2Fstorage.azure.com%2F", 400)]
    [InlineData("GET", "true", "?resource=https%3A%2F%2Fstorage.azure.com%2F", 400)]
    [InlineData("GET", "true", "?api-version=2018-02-01", 400)]
    [InlineData("GET", "true", "?api-version=2018-02-01&resource=invalid", 400)]
    [InlineData("POST", "true", "?api-version=2018-02-01&resource=https%3A%2F%2Fstorage.azure.com%2F", 405)]
    public async Task InvalidTokenRequestsAreRejected(string method, string metadata, string query, int status)
    {
        var called = false;
        var middleware = new ImdsMiddleware(_ => { called = true; return Task.CompletedTask; }, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/metadata/identity/oauth2/token";
        context.Request.Headers["Metadata"] = metadata;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        Assert.False(called);
        Assert.Equal(status, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ForImdsPath_ReturnsTokenJson_WithoutCallingNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new ImdsMiddleware(next, new InMemoryConfig("AzLocal:FakeToken", "my-fake-token"));
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/metadata/identity/oauth2/token";
        context.Request.Headers["Metadata"] = "true";
        context.Request.QueryString = new QueryString("?api-version=2018-02-01&resource=https%3A%2F%2Fmanagement.azure.com%2F");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = JsonDocument.Parse(context.Response.Body);
        Assert.Equal("my-fake-token", doc.RootElement.GetProperty("access_token").GetString());
        Assert.Equal("Bearer", doc.RootElement.GetProperty("token_type").GetString());
        Assert.Equal("https://management.azure.com/", doc.RootElement.GetProperty("resource").GetString());
        Assert.InRange(long.Parse(doc.RootElement.GetProperty("expires_on").GetString()!), DateTimeOffset.UtcNow.AddMinutes(59).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.AddMinutes(61).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task InvokeAsync_ForImdsPath_WithoutConfiguredToken_UsesDefaultFakeToken()
    {
        var middleware = new ImdsMiddleware(_ => Task.CompletedTask, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/metadata/identity/oauth2/token";
        context.Request.Headers["Metadata"] = "true";
        context.Request.QueryString = new QueryString("?api-version=2018-02-01&resource=https%3A%2F%2Fmanagement.azure.com%2F");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = JsonDocument.Parse(context.Response.Body);
        Assert.Equal(AzLocal.Core.EmulatorDefaults.FakeToken, doc.RootElement.GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ForSubPath_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new ImdsMiddleware(next, new InMemoryConfig());
        var context = new DefaultHttpContext();
        context.Request.Path = "/metadata/identity/oauth2/token/extra";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(called);
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
