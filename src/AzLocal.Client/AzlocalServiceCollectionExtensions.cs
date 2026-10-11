using AzLocal.Client;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class AzlocalServiceCollectionExtensions
{
    /// <summary>
    /// Registers reusable local Azure SDK clients as singletons. Blob and Key Vault
    /// clients are registered when their resource names are supplied. Existing
    /// registrations are preserved. The container owns registered Service Bus clients.
    /// </summary>
    public static IServiceCollection AddAzLocal(this IServiceCollection services,
        Action<AzlocalClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var input = new AzlocalClientOptions();
        configure?.Invoke(input);
        // Keep registrations independent of subsequent mutations to the input object.
        var options = input with { };
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !System.Net.IPAddress.TryParse(endpoint.Host, out var address) ||
            !System.Net.IPAddress.IsLoopback(address) ||
            endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
            throw new ArgumentException("BaseUrl must be an HTTPS loopback IP endpoint, such as https://127.0.0.1:4566.", nameof(configure));
        if (options.AmqpPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(configure), "AmqpPort must be between 1 and 65535.");
        if (options.BlobContainerName is not null && string.IsNullOrWhiteSpace(options.BlobAccountName))
            throw new ArgumentException("BlobAccountName is required when BlobContainerName is supplied.", nameof(configure));
        foreach (var name in new[] { options.BlobAccountName, options.BlobContainerName, options.KeyVaultName, options.ServiceBusQueueName, options.SubscriptionId })
            if (name is not null && string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configured resource names cannot be empty.", nameof(configure));

        services.TryAddSingleton(_ => new AzlocalClientFactory(options.BaseUrl, options.TransportHandler));
        services.TryAddSingleton<AzlocalCredential>(provider => provider.GetRequiredService<AzlocalClientFactory>().Credential);
        services.TryAddSingleton<TokenCredential>(provider => provider.GetRequiredService<AzlocalCredential>());
        services.TryAddSingleton<ManagedIdentityCredential>(provider => provider.GetRequiredService<AzlocalClientFactory>().CreateManagedIdentityCredential());
        services.TryAddSingleton<ArmClient>(provider => provider.GetRequiredService<AzlocalClientFactory>()
            .CreateArmClient(options.SubscriptionId, provider.GetRequiredService<TokenCredential>()));
        services.TryAddSingleton<ServiceBusClient>(provider => provider.GetRequiredService<AzlocalClientFactory>().CreateServiceBusClient(options.AmqpPort));

        if (options.BlobAccountName is { } account)
            services.TryAddSingleton<BlobServiceClient>(provider => provider.GetRequiredService<AzlocalClientFactory>().CreateBlobServiceClient(account));
        if (options.BlobAccountName is { } containerAccount && options.BlobContainerName is { } container)
            services.TryAddSingleton<BlobContainerClient>(provider => provider.GetRequiredService<AzlocalClientFactory>().CreateBlobContainerClient(containerAccount, container));
        if (options.KeyVaultName is { } vault)
            services.TryAddSingleton<SecretClient>(provider => provider.GetRequiredService<AzlocalClientFactory>().CreateSecretClient(vault));
        if (options.ServiceBusQueueName is { } queue)
        {
            services.TryAddSingleton<ServiceBusSender>(provider => provider.GetRequiredService<ServiceBusClient>().CreateSender(queue));
            services.TryAddSingleton<ServiceBusReceiver>(provider => provider.GetRequiredService<ServiceBusClient>().CreateReceiver(queue));
        }
        return services;
    }
}
