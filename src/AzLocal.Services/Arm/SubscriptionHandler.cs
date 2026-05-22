using AzLocal.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzLocal.Services.Arm;

/// <summary>
/// Emulates the ARM Subscriptions API. Returns a single hardcoded fake subscription
/// so the Azure SDK can resolve subscription context without a real Azure tenant.
/// </summary>
public class SubscriptionHandler : IServiceHandler
{
    private readonly ILogger<SubscriptionHandler> _logger;

    public string ServiceName => "ARM/Subscriptions";

    // Fake subscription returned for every request — matches the fake tenant from AuthStubMiddleware.
    private static readonly object FakeSubscription = new
    {
        subscriptionId = "00000000-0000-0000-0000-000000000001",
        displayName = "AzLocal Dev Subscription",
        state = "Enabled",
        tenantId = "00000000-0000-0000-0000-000000000002"
    };

    public SubscriptionHandler(ILogger<SubscriptionHandler> logger)
    {
        _logger = logger;
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(ArmRoutes.Subscriptions, (Delegate)ListSubscriptionsAsync);
        app.MapGet(ArmRoutes.Subscription, GetSubscriptionAsync);
    }

    /// <summary>Returns the single fake subscription as a paged ARM list response.</summary>
    private Task<IResult> ListSubscriptionsAsync(HttpContext ctx)
    {
        _logger.LogDebug("ListSubscriptions");
        SetRequestId(ctx);
        return Task.FromResult(Results.Ok(new { value = new[] { FakeSubscription } }));
    }

    /// <summary>Returns the fake subscription if the requested ID matches, otherwise 404.</summary>
    private Task<IResult> GetSubscriptionAsync(string subscriptionId, HttpContext ctx)
    {
        SetRequestId(ctx);
        if (!subscriptionId.Equals("00000000-0000-0000-0000-000000000001", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("GetSubscription not found subscriptionId={SubscriptionId}", subscriptionId);
            return Task.FromResult(Results.NotFound());
        }

        _logger.LogDebug("GetSubscription subscriptionId={SubscriptionId}", subscriptionId);
        return Task.FromResult(Results.Ok(FakeSubscription));
    }

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();
}
