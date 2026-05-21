using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace AzLocal.Core.Routing;

public static class ServiceRouter
{
    private static readonly Regex SubscriptionPattern =
       new(@"/subscriptions/([^/]+)", RegexOptions.Compiled);
    private static readonly Regex ResourceGroupPattern =
        new(@"/resourceGroups/([^/]+)", RegexOptions.Compiled);

    public static RequestContext BuildContext(HttpRequest request)
    {
        var ctx = new RequestContext();
        var path = request.Path.Value ?? string.Empty;
        var host = request.Host.Host;

        var subMatch = SubscriptionPattern.Match(path);
        if (subMatch.Success)
            ctx.SubscriptionId = subMatch.Groups[1].Value;

        var rgMatch = ResourceGroupPattern.Match(path);
        if (rgMatch.Success)
            ctx.ResourceGroup = rgMatch.Groups[1].Value;

        if (host.Contains(".blob.core.windows.net"))
        {
            ctx.ServiceType = "blob";
            ctx.AccountName = host.Split('.')[0];
        }
        else if (host.Contains(".vault.azure.net"))
        {
            ctx.ServiceType = "keyvault";
            ctx.AccountName = host.Split('.')[0];
        }
        else if (host.Contains(".servicebus.windows.net"))
        {
            ctx.ServiceType = "servicebus";
            ctx.AccountName = host.Split('.')[0];
        }
        else
        {
            ctx.ServiceType = "arm";
        }

        ctx.ApiVersion = request.Query["api-version"].FirstOrDefault() ?? string.Empty;
        return ctx;
    }
}

