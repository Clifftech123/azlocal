using AzLocal.Core.Interfaces;
using AzLocal.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzLocal.Services.Arm;

/// <summary>
/// Emulates the ARM Resource Groups API. Supports create, get, list, and delete
/// so the Azure SDK can manage resource group lifecycle against the local emulator.
/// </summary>
public class ResourceGroupHandler : IServiceHandler
{
    private readonly IStateStore _state;
    private readonly ILogger<ResourceGroupHandler> _logger;

    public string ServiceName => "ARM/ResourceGroups";

    public ResourceGroupHandler(IStateStore state, ILogger<ResourceGroupHandler> logger)
    {
        _state = state;
        _logger = logger;
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(ArmRoutes.ResourceGroups,   ListResourceGroupsAsync);
        app.MapGet(ArmRoutes.ResourceGroup,    GetResourceGroupAsync);
        app.MapPut(ArmRoutes.ResourceGroup,    CreateOrUpdateResourceGroupAsync);
        app.MapDelete(ArmRoutes.ResourceGroup, DeleteResourceGroupAsync);
    }

    /// <summary>Returns all resource groups for the given subscription as an ARM paged list.</summary>
    private async Task<IResult> ListResourceGroupsAsync(string subscriptionId, HttpContext ctx)
    {
        var groups = await _state.ListAsync<ResourceGroup>($"arm/resourcegroups/{subscriptionId}/");
        _logger.LogDebug("ListResourceGroups subscriptionId={Sub} count={Count}", subscriptionId, groups.Count);

        SetRequestId(ctx);
        return Results.Ok(new { value = groups.Select(g => ResourceGroupResponse(subscriptionId, g)) });
    }

    /// <summary>Returns a single resource group by name, or 404 if it does not exist.</summary>
    private async Task<IResult> GetResourceGroupAsync(string subscriptionId, string resourceGroupName, HttpContext ctx)
    {
        var rg = await _state.GetAsync<ResourceGroup>(RgKey(subscriptionId, resourceGroupName));
        if (rg is null)
        {
            _logger.LogWarning("GetResourceGroup not found sub={Sub} rg={RG}", subscriptionId, resourceGroupName);
            return Results.NotFound();
        }

        SetRequestId(ctx);
        return Results.Ok(ResourceGroupResponse(subscriptionId, rg));
    }

    /// <summary>Creates or updates a resource group. Reads location from the request body.</summary>
    private async Task<IResult> CreateOrUpdateResourceGroupAsync(
        string subscriptionId, string resourceGroupName, HttpContext ctx)
    {
        string location = "local";
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            if (doc.RootElement.TryGetProperty("location", out var loc))
                location = loc.GetString() ?? "local";
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "CreateResourceGroup could not parse body — using default location");
        }

        var existing = await _state.GetAsync<ResourceGroup>(RgKey(subscriptionId, resourceGroupName));
        var isNew = existing is null;

        var rg = new ResourceGroup
        {
            Name             = resourceGroupName,
            Location         = location,
            SubscriptionId   = subscriptionId,
            ProvisioningState = "Succeeded"
        };

        await _state.SetAsync(RgKey(subscriptionId, resourceGroupName), rg);
        _logger.LogInformation("ResourceGroup {Action} sub={Sub} rg={RG} location={Location}",
            isNew ? "created" : "updated", subscriptionId, resourceGroupName, location);

        SetRequestId(ctx);
        return isNew ? Results.Created() : Results.Ok(ResourceGroupResponse(subscriptionId, rg));
    }

    /// <summary>Deletes a resource group. No-ops if it does not exist.</summary>
    private async Task<IResult> DeleteResourceGroupAsync(string subscriptionId, string resourceGroupName, HttpContext ctx)
    {
        await _state.DeleteAsync(RgKey(subscriptionId, resourceGroupName));
        _logger.LogInformation("ResourceGroup deleted sub={Sub} rg={RG}", subscriptionId, resourceGroupName);

        SetRequestId(ctx);
        return Results.Accepted();
    }

    #region Private helpers

    private static string RgKey(string subscriptionId, string name) =>
        $"arm/resourcegroups/{subscriptionId}/{name}".ToLowerInvariant();

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();

    private static object ResourceGroupResponse(string subscriptionId, ResourceGroup rg) => new
    {
        id       = $"/subscriptions/{subscriptionId}/resourceGroups/{rg.Name}",
        name     = rg.Name,
        location = rg.Location,
        tags     = rg.Tags,
        properties = new { provisioningState = rg.ProvisioningState }
    };

    #endregion
}
