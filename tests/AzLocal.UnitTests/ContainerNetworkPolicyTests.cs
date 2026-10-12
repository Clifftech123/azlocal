using AzLocal.Core;
using AzLocal.UnitTests.TestHelpers;
using System.Net;

namespace AzLocal.UnitTests;

public class ContainerNetworkPolicyTests
{
    [Theory]
    [InlineData("true", true, true)]
    [InlineData("true", false, false)]
    [InlineData("false", true, false)]
    [InlineData(null, true, false)]
    public void IngressRequiresOptInAndContainerRuntime(string? setting, bool runtime, bool expected)
    {
        Assert.Equal(expected, ContainerNetworkPolicy.IsEnabled(new InMemoryConfig("AzLocal:ContainerNetworking", setting), runtime));
    }

    [Theory]
    [InlineData("127.0.0.1", false, true)]
    [InlineData("172.18.0.2", false, false)]
    [InlineData("172.18.0.2", true, true)]
    [InlineData("192.168.65.1", true, true)]
    [InlineData("::ffff:172.18.0.2", true, true)]
    [InlineData("fd00::1", true, true)]
    [InlineData("203.0.113.1", true, false)]
    [InlineData("172.32.0.1", true, false)]
    [InlineData("2001:db8::1", true, false)]
    public void ContainerIngressAllowsPrivatePeersOnly(string address, bool container, bool expected)
    {
        Assert.Equal(expected, ContainerNetworkPolicy.AllowsClient(IPAddress.Parse(address), container));
    }

    [Theory]
    [InlineData("https://0.0.0.0:4566", false, false)]
    [InlineData("https://0.0.0.0:4566", true, true)]
    [InlineData("https://127.0.0.1:4566", false, true)]
    [InlineData("https://example.com:4566", true, false)]
    public void WildcardListenersRequireContainerMode(string address, bool container, bool expected)
    {
        Assert.Equal(expected, ContainerNetworkPolicy.AllowsListener(new Uri(address), container));
    }
}
