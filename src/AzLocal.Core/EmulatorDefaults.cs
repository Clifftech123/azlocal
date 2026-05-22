namespace AzLocal.Core;

/// <summary>
/// Single source of truth for default values shared across the emulator host and client library.
/// All defaults match the values in appsettings.json — change them there to override at runtime.
/// </summary>
public static class EmulatorDefaults
{
    public const string FakeToken      = "fake-azlocal-token";
    public const string BaseUrl        = "http://localhost:4566";
    public const string ObjectId       = "00000000-0000-0000-0000-000000000001";
    public const string TenantId       = "00000000-0000-0000-0000-000000000002";
    public const string SubscriptionId = "00000000-0000-0000-0000-000000000001";
}
