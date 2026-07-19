using AzLocal.Core.Routing;
using Microsoft.AspNetCore.Http;

namespace AzLocal.UnitTests;

public class ServiceRouterTests
{
    private static HttpRequest NewRequest(string host, string path, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = new PathString(path);
        if (queryString is not null)
            context.Request.QueryString = new QueryString(queryString);
        return context.Request;
    }

    [Theory]
    [InlineData("myaccount.blob.core.windows.net", "blob")]
    [InlineData("myvault.vault.azure.net", "keyvault")]
    [InlineData("myns.servicebus.windows.net", "servicebus")]
    [InlineData("management.azure.com", "arm")]
    [InlineData("localhost", "arm")]
    public void BuildContext_SetsServiceType_FromHost(string host, string expectedServiceType)
    {
        var ctx = ServiceRouter.BuildContext(NewRequest(host, "/"));

        Assert.Equal(expectedServiceType, ctx.ServiceType);
    }

    [Theory]
    [InlineData("myaccount.blob.core.windows.net", "myaccount")]
    [InlineData("myvault.vault.azure.net", "myvault")]
    [InlineData("myns.servicebus.windows.net", "myns")]
    public void BuildContext_SetsAccountName_FromFirstHostLabel(string host, string expectedAccount)
    {
        var ctx = ServiceRouter.BuildContext(NewRequest(host, "/"));

        Assert.Equal(expectedAccount, ctx.AccountName);
    }

    [Fact]
    public void BuildContext_ForNonStorageHost_LeavesAccountNameEmpty()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("management.azure.com", "/"));

        Assert.Equal(string.Empty, ctx.AccountName);
    }

    [Fact]
    public void BuildContext_ExtractsSubscriptionId_FromPath()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/subscriptions/sub-123/resourceGroups/my-rg"));

        Assert.Equal("sub-123", ctx.SubscriptionId);
    }

    [Fact]
    public void BuildContext_ExtractsResourceGroup_FromPath()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/subscriptions/sub-123/resourceGroups/my-rg"));

        Assert.Equal("my-rg", ctx.ResourceGroup);
    }

    [Fact]
    public void BuildContext_WhenPathHasNeitherSegment_LeavesBothEmpty()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/kv/myvault/secrets/mysecret"));

        Assert.Equal(string.Empty, ctx.SubscriptionId);
        Assert.Equal(string.Empty, ctx.ResourceGroup);
    }

    [Fact]
    public void BuildContext_IsCaseSensitive_ForResourceGroupsSegment()
    {
        // Real ARM paths always use "resourceGroups" (capital G) — lowercase should not match.
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/subscriptions/sub-123/resourcegroups/my-rg"));

        Assert.Equal("sub-123", ctx.SubscriptionId);
        Assert.Equal(string.Empty, ctx.ResourceGroup);
    }

    [Fact]
    public void BuildContext_ExtractsApiVersion_FromQueryString()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/", "?api-version=2023-01-01"));

        Assert.Equal("2023-01-01", ctx.ApiVersion);
    }

    [Fact]
    public void BuildContext_WhenApiVersionMissing_LeavesItEmpty()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/", "?other=value"));

        Assert.Equal(string.Empty, ctx.ApiVersion);
    }

    [Fact]
    public void BuildContext_WhenQueryStringAbsent_ApiVersionIsEmpty()
    {
        var ctx = ServiceRouter.BuildContext(NewRequest("localhost", "/"));

        Assert.Equal(string.Empty, ctx.ApiVersion);
    }
}
