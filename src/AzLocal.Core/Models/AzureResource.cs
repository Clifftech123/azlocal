namespace AzLocal.Core.Models;

public abstract class AzureResource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
}
