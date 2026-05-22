namespace AzLocal.Services.Arm;

/// <summary>
/// Route constants for the ARM (Azure Resource Manager) emulator endpoints.
/// Mirrors the real ARM REST API path structure under the /arm prefix.
/// </summary>
public static class ArmRoutes
{
    public const string Subscriptions  = "/arm/subscriptions";
    public const string Subscription   = "/arm/subscriptions/{subscriptionId}";
    public const string ResourceGroups = "/arm/subscriptions/{subscriptionId}/resourcegroups";
    public const string ResourceGroup  = "/arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}";
}
