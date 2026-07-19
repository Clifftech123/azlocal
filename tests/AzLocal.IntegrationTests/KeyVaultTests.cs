using AzLocal.IntegrationTests.Fixtures;
using Azure;

namespace AzLocal.IntegrationTests;

public class KeyVaultTests : IClassFixture<EmulatorFixture>
{
    private readonly EmulatorFixture _fixture;

    public KeyVaultTests(EmulatorFixture fixture) => _fixture = fixture;

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task SetSecret_ThenGetSecret_RoundTripsValue()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");

        await secrets.SetSecretAsync(name, "super-secret-value");
        var result = await secrets.GetSecretAsync(name);

        Assert.Equal("super-secret-value", result.Value.Value);
    }

    [Fact]
    public async Task SetSecret_Twice_LatestVersionWins()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");

        await secrets.SetSecretAsync(name, "first-value");
        await secrets.SetSecretAsync(name, "second-value");
        var result = await secrets.GetSecretAsync(name);

        Assert.Equal("second-value", result.Value.Value);
    }

    [Fact]
    public async Task GetSecret_ThatWasNeverSet_Throws404()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");

        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => secrets.GetSecretAsync(UniqueName("missing")));

        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task DeleteSecret_MakesItUnavailable()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        await secrets.SetSecretAsync(name, "to-be-deleted");

        await secrets.StartDeleteSecretAsync(name);

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => secrets.GetSecretAsync(name));
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task ListPropertiesOfSecrets_ReturnsSetSecret()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        await secrets.SetSecretAsync(name, "listed-value");

        var names = new List<string>();
        await foreach (var props in secrets.GetPropertiesOfSecretsAsync())
            names.Add(props.Name);

        Assert.Contains(name, names);
    }

    [Fact]
    public async Task GetSecret_WithExplicitVersion_ReturnsThatVersion()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        var firstSet = await secrets.SetSecretAsync(name, "version-one");
        await secrets.SetSecretAsync(name, "version-two");

        var result = await secrets.GetSecretAsync(name, firstSet.Value.Properties.Version);

        Assert.Equal("version-one", result.Value.Value);
    }


    [Fact]
    public async Task ListPropertiesOfSecrets_ShowsOnlyLatestVersion_NotEveryVersion()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        await secrets.SetSecretAsync(name, "v1");
        await secrets.SetSecretAsync(name, "v2");
        await secrets.SetSecretAsync(name, "v3");

        var matches = new List<string>();
        await foreach (var props in secrets.GetPropertiesOfSecretsAsync())
            if (props.Name == name) matches.Add(props.Version);

        Assert.Single(matches);
    }

    [Fact]
    public async Task ListPropertiesOfSecrets_ExcludesDeletedSecret()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        await secrets.SetSecretAsync(name, "will-be-deleted");
        await secrets.StartDeleteSecretAsync(name);

        var names = new List<string>();
        await foreach (var props in secrets.GetPropertiesOfSecretsAsync())
            names.Add(props.Name);

        Assert.DoesNotContain(name, names);
    }

    [Fact]
    public async Task SetSecret_WithEmptyValue_RoundTrips()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");

        await secrets.SetSecretAsync(name, string.Empty);
        var result = await secrets.GetSecretAsync(name);

        Assert.Equal(string.Empty, result.Value.Value);
    }

    [Fact]
    public async Task Secrets_AreIsolatedAcrossVaults()
    {
        var name = UniqueName("secret");
        var vaultA = _fixture.Clients.CreateSecretClient(UniqueName("vaultA"));
        var vaultB = _fixture.Clients.CreateSecretClient(UniqueName("vaultB"));
        await vaultA.SetSecretAsync(name, "only-in-vault-a");

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => vaultB.GetSecretAsync(name));

        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task DeleteSecret_ThenSetSecretAgain_MakesItAvailableAgain()
    {
        var secrets = _fixture.Clients.CreateSecretClient("myvault");
        var name = UniqueName("secret");
        await secrets.SetSecretAsync(name, "first-life");
        await secrets.StartDeleteSecretAsync(name);

        await secrets.SetSecretAsync(name, "second-life");
        var result = await secrets.GetSecretAsync(name);

        Assert.Equal("second-life", result.Value.Value);
    }
}
