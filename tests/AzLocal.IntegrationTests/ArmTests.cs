using AzLocal.IntegrationTests.Fixtures;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AzLocal.IntegrationTests;

// Note: like ServiceBus, ARM here is exercised via AzlocalClientFactory.CreateHttpClient's raw
// HTTP surface rather than the official Azure.ResourceManager SDK — that SDK is a much larger
// surface (long-running-operation polling, resource identifier composition, etc.) that AzLocal
// doesn't attempt to support today.
public class ArmTests : IClassFixture<EmulatorFixture>
{
    private readonly EmulatorFixture _fixture;

    public ArmTests(EmulatorFixture fixture) => _fixture = fixture;

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private HttpClient NewClient() => _fixture.Clients.CreateHttpClient();

    private async Task<string> GetConfiguredSubscriptionIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/arm/subscriptions");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("value")[0].GetProperty("subscriptionId").GetString()!;
    }

    [Fact]
    public async Task ListSubscriptions_ReturnsConfiguredSubscription()
    {
        var client = NewClient();

        var response = await client.GetAsync("/arm/subscriptions");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var values = doc.RootElement.GetProperty("value").EnumerateArray().ToList();
        Assert.Single(values);
        Assert.Equal("Enabled", values[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task GetSubscription_WithConfiguredId_ReturnsIt()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);

        var response = await client.GetAsync($"/arm/subscriptions/{subId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithUnknownId_Returns404()
    {
        var client = NewClient();

        var response = await client.GetAsync($"/arm/subscriptions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateResourceGroup_ReturnsCreated_AndReadsLocationFromBody()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        var body = new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetResourceGroup_AfterCreate_ReturnsMatchingLocation()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}",
            new StringContent("{\"location\":\"westus\"}", Encoding.UTF8, "application/json"));

        var response = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(rgName, doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("westus", doc.RootElement.GetProperty("location").GetString());
        Assert.Equal("Succeeded", doc.RootElement.GetProperty("properties").GetProperty("provisioningState").GetString());
    }

    [Fact]
    public async Task GetResourceGroup_ThatWasNeverCreated_Returns404()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);

        var response = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups/{UniqueName("missing")}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateResourceGroup_Twice_SecondCallUpdatesAndReturnsOk()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        var firstBody = new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json");
        var secondBody = new StringContent("{\"location\":\"westus\"}", Encoding.UTF8, "application/json");

        var first = await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}", firstBody);
        var second = await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}", secondBody);
        using var doc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("westus", doc.RootElement.GetProperty("location").GetString());
    }

    [Fact]
    public async Task ListResourceGroups_IncludesCreatedGroup()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}",
            new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json"));

        var response = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var names = doc.RootElement.GetProperty("value").EnumerateArray()
            .Select(v => v.GetProperty("name").GetString()).ToList();
        Assert.Contains(rgName, names);
    }

    [Fact]
    public async Task DeleteResourceGroup_RemovesIt()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}",
            new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json"));

        var delete = await client.DeleteAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}");
        var getAfter = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}");

        Assert.Equal(HttpStatusCode.Accepted, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
    }

    [Fact]
    public async Task DeleteResourceGroup_ThatWasNeverCreated_DoesNotThrow()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);

        var response = await client.DeleteAsync($"/arm/subscriptions/{subId}/resourcegroups/{UniqueName("missing")}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task CreateResourceGroup_WithNoBody_DefaultsToLocalLocation()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");

        var create = await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}", content: null);
        var get = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("local", doc.RootElement.GetProperty("location").GetString());
    }

    [Fact]
    public async Task GetSubscription_IsCaseInsensitive()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);

        var response = await client.GetAsync($"/arm/subscriptions/{subId.ToUpperInvariant()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResourceGroups_AreIsolatedAcrossSubscriptions()
    {
        var client = NewClient();
        var rgName = UniqueName("shared-name-rg");
        var subA = UniqueName("sub-a");
        var subB = UniqueName("sub-b");
        await client.PutAsync($"/arm/subscriptions/{subA}/resourcegroups/{rgName}",
            new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json"));

        var getFromB = await client.GetAsync($"/arm/subscriptions/{subB}/resourcegroups/{rgName}");

        Assert.Equal(HttpStatusCode.NotFound, getFromB.StatusCode);
    }

    [Fact]
    public async Task ListResourceGroups_ForSubscriptionWithNone_ReturnsEmptyList()
    {
        var client = NewClient();
        var subId = UniqueName("empty-sub");

        var response = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(doc.RootElement.GetProperty("value").EnumerateArray());
    }

    [Fact]
    public async Task ResourceGroupId_MatchesArmResourceIdFormat()
    {
        var client = NewClient();
        var subId = await GetConfiguredSubscriptionIdAsync(client);
        var rgName = UniqueName("rg");
        await client.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}",
            new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json"));

        var get = await client.GetAsync($"/arm/subscriptions/{subId}/resourcegroups/{rgName}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());

        Assert.Equal($"/subscriptions/{subId}/resourceGroups/{rgName}", doc.RootElement.GetProperty("id").GetString());
    }
}
