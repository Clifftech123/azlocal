using AzLocal.Core.Routing;
using AzLocal.Middleware;
using Microsoft.AspNetCore.Http;

namespace AzLocal.UnitTests;

public class RequestContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_StoresRequestContext_InHttpContextItems()
    {
        var middleware = new RequestContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("myaccount.blob.core.windows.net");
        context.Request.Path = "/subscriptions/sub-1/resourceGroups/my-rg";

        await middleware.InvokeAsync(context);

        var stored = Assert.IsType<RequestContext>(context.Items["RequestContext"]);
        Assert.Equal("blob", stored.ServiceType);
        Assert.Equal("myaccount", stored.AccountName);
        Assert.Equal("sub-1", stored.SubscriptionId);
        Assert.Equal("my-rg", stored.ResourceGroup);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var middleware = new RequestContextMiddleware(next);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }
}
