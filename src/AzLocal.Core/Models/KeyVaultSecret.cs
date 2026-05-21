namespace AzLocal.Core.Models;

public class KeyVaultSecret : AzureResource
{
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresOn { get; set; }
    // Disabled secrets exist in storage but cannot be read
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.UtcNow;
    // New GUID for each version — stored as part of the state store key
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}
