namespace AzLocal.Core.Models;

public class KeyVaultSecret : AzureResource
{
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresOn { get; set; }
}
