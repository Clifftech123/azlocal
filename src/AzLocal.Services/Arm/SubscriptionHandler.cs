using AzLocal.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzLocal.Services.Arm;

/// <summary>
/// Emulates the ARM Subscriptions API. Returns a single fake subscription sourced from
/// <c>AzLocal:SubscriptionId</c> and <c>AzLocal:TenantId</c> in configuration so the
/// Azure SDK can resolve subscription context without a real Azure tenant.
/// </summary>
public class SubscriptionHandler : IServiceHandler
{
    private readonly ILogger<SubscriptionHandler> _logger;
    private readonly object _fakeSubscription;
    private readonly string _subscriptionId;

    public string ServiceName => "ARM/Subscriptions";

    public SubscriptionHandler(IConfiguration config, ILogger<SubscriptionHandler> logger)
    {
        _logger = logger;
        _subscriptionId = config["AzLocal:SubscriptionId"] ?? "00000000-0000-0000-0000-000000000001";
        var tenantId     = config["AzLocal:TenantId"]      ?? "00000000-0000-0000-0000-000000000002";

        _fakeSubscription = new
        {
            subscriptionId = _subscriptionId,
            displayName    = config["AzLocal:SubscriptionDisplayName"] ?? "AzLocal Dev Subscription",
            state          = "Enabled",
            tenantId
        };
    }

    public void MapRoutes(WebApplication app)
    {
        // Cast to Delegate so the framework treats this as a route handler and writes the
        // IResult to the response — without the cast, the HttpContext-only signature matches
        // RequestDelegate and the return value is silently discarded.
        app.MapGet(ArmRoutes.Subscriptions, (Delegate)ListSubscriptionsAsync);
        app.MapGet(ArmRoutes.Subscription,  GetSubscriptionAsync);
    }

    /// <summary>Returns the configured fake subscription as a paged ARM list response.</summary>
    private Task<IResult> ListSubscriptionsAsync(HttpContext ctx)
    {
        _logger.LogDebug("ListSubscriptions");
        SetRequestId(ctx);
        return Task.FromResult(Results.Ok(new { value = new[] { _fakeSubscription } }));
    }

    /// <summary>Returns the fake subscription if the requested ID matches, otherwise 404.</summary>
    private Task<IResult> GetSubscriptionAsync(string subscriptionId, HttpContext ctx)
    {
        SetRequestId(ctx);
        if (!subscriptionId.Equals(_subscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("GetSubscription not found subscriptionId={SubscriptionId}", subscriptionId);
            return Task.FromResult(Results.NotFound());
        }

        _logger.LogDebug("GetSubscription subscriptionId={SubscriptionId}", subscriptionId);
        return Task.FromResult(Results.Ok(_fakeSubscription));
    }

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();
}
