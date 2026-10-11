using AzLocal.Client;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Resources;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AzLocal.CompatTests;

public class ArmSdkTests : IClassFixture<CompatFixture>
{
    private readonly AzlocalClientFactory _clients;
    public ArmSdkTests(CompatFixture host) =>
        _clients = new AzlocalClientFactory("https://127.0.0.1", host.Server.CreateHandler());

    [Fact]
    public async Task OfficialSdkManagesResourceGroupLifecycle()
    {
        var arm = _clients.CreateArmClient();
        var subscription = await arm.GetDefaultSubscriptionAsync();
        Assert.Equal("00000000-0000-0000-0000-000000000001", subscription.Data.SubscriptionId);
        var groups = subscription.GetResourceGroups();
        var name = "sdk-" + Guid.NewGuid().ToString("N");
        var data = new ResourceGroupData(new AzureLocation("eastus"));
        data.Tags["owner"] = "test";
        var created = await groups.CreateOrUpdateAsync(WaitUntil.Completed, name, data);
        Assert.True(created.HasCompleted);
        Assert.Equal(name, created.Value.Data.Name);
        Assert.Equal("test", created.Value.Data.Tags["owner"]);
        Assert.True((await groups.ExistsAsync(name)).Value);
        Assert.Equal("eastus", (await groups.GetAsync(name)).Value.Data.Location.Name);
        var listed = new List<string>();
        await foreach (var group in groups.GetAllAsync()) listed.Add(group.Data.Name);
        Assert.Contains(name, listed);
        await created.Value.DeleteAsync(WaitUntil.Completed);
        Assert.False((await groups.ExistsAsync(name)).Value);
    }
}
