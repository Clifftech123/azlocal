using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;

namespace AzLocal.Core;

/// <summary>Explicit networking policy for local container deployments.</summary>
public static class ContainerNetworkPolicy
{
    /// <summary>Container ingress requires both the opt-in and a container runtime.</summary>
    public static bool IsEnabled(IConfiguration config) =>
        IsEnabled(config, string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>Evaluates the opt-in against a known runtime context.</summary>
    public static bool IsEnabled(IConfiguration config, bool runningInContainer) =>
        runningInContainer && string.Equals(config["AzLocal:ContainerNetworking"], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Permits loopback, or private network ingress in container mode.</summary>
    public static bool AllowsClient(IPAddress? address, bool containerNetworking)
    {
        if (address is null) return true; // In-process test servers have no peer IP.
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (!containerNetworking) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168;
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xfe) == 0xfc;
    }

    /// <summary>Validates listener addresses without opening native runs to a network.</summary>
    public static bool AllowsListener(Uri uri, bool containerNetworking)
    {
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)) return false;
        return IPAddress.IsLoopback(address) || containerNetworking && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
    }
}
