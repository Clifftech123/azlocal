using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;

namespace AzLocal.CompatTests;

public class ManagedIdentitySdkTests : IClassFixture<CompatFixture>
{
    private readonly CompatFixture _host;
    public ManagedIdentitySdkTests(CompatFixture host) => _host = host;

    [Fact]
    public async Task OfficialCredentialAcquiresLocalToken()
    {
        var credential = _host.Clients.CreateManagedIdentityCredential();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), timeout.Token);
        Assert.Equal(AzLocal.Core.EmulatorDefaults.FakeToken, token.Token);
        Assert.True(token.ExpiresOn > DateTimeOffset.UtcNow);
        var arm = _host.Clients.CreateArmClient(credential: credential);
        Assert.NotNull(await arm.GetDefaultSubscriptionAsync(timeout.Token));
    }
}
