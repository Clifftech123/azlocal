namespace AzLocal.Services.KeyVault;

/// <summary>
/// Route constants for the Key Vault Secrets emulator endpoints.
/// <c>{vault}</c> is the vault name, matching how the real Key Vault API scopes secrets per vault.
/// </summary>
public static class KeyVaultRoutes
{
    public const string Secrets       = "/kv/{vault}/secrets";
    public const string SecretByName  = "/kv/{vault}/secrets/{secretName}";
    public const string SecretVersion = "/kv/{vault}/secrets/{secretName}/{version}";
}
