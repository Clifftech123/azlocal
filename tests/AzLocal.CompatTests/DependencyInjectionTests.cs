using AzLocal.Client;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace AzLocal.CompatTests;

public class DependencyInjectionTests : IClassFixture<CompatFixture>
{
    private readonly CompatFixture _host;
    public DependencyInjectionTests(CompatFixture host) => _host = host;

    [Fact]
    public async Task InjectedClientsWorkAndAreSharedAcrossScopes()
    {
        var name = "di-" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddAzLocal(options =>
        {
            options.BlobAccountName = "diaccount";
            options.BlobContainerName = name;
            options.KeyVaultName = name;
            options.ServiceBusQueueName = name;
            options.AmqpPort = _host.AmqpPort;
            options.TransportHandler = _host.Server.CreateHandler();
        });
        services.AddScoped<Consumer>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var consumer = first.ServiceProvider.GetRequiredService<Consumer>();
        var other = second.ServiceProvider.GetRequiredService<Consumer>();
        Assert.NotSame(consumer, other);
        Assert.Same(consumer.Blobs, other.Blobs);
        Assert.Same(consumer.Secrets, other.Secrets);
        Assert.Same(consumer.Sender, other.Sender);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        await consumer.Blobs.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = consumer.Blobs.GetBlobClient("example.txt");
        await blob.UploadAsync(new BinaryData("injected"), cancellationToken: ct);
        Assert.Equal("injected", (await blob.DownloadContentAsync(ct)).Value.Content.ToString());
        await consumer.Secrets.SetSecretAsync("test", "injected", ct);
        Assert.Equal("injected", (await consumer.Secrets.GetSecretAsync("test", cancellationToken: ct)).Value.Value);
        await consumer.Sender.SendMessageAsync(new ServiceBusMessage("injected"), ct);
        var receiver = provider.GetRequiredService<ServiceBusReceiver>();
        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(message);
        Assert.Equal("injected", message.Body.ToString());
        await receiver.CompleteMessageAsync(message, ct);
        Assert.NotNull(await provider.GetRequiredService<ArmClient>().GetDefaultSubscriptionAsync(ct));
        Assert.Same(provider.GetRequiredService<TokenCredential>(), provider.GetRequiredService<AzlocalCredential>());
    }

    [Fact]
    public async Task ManagedIdentityCanBeSelectedThroughDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TokenCredential>(provider => provider.GetRequiredService<Azure.Identity.ManagedIdentityCredential>());
        services.AddAzLocal(options => options.TransportHandler = _host.Server.CreateHandler());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.IsType<Azure.Identity.ManagedIdentityCredential>(provider.GetRequiredService<TokenCredential>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Assert.NotNull(await provider.GetRequiredService<ArmClient>().GetDefaultSubscriptionAsync(deadline.Token));
    }

    [Fact]
    public async Task ContainerDisposesServiceBusClientsAndLinks()
    {
        var services = new ServiceCollection().AddAzLocal(options => options.ServiceBusQueueName = "disposal");
        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<ServiceBusClient>();
        var sender = provider.GetRequiredService<ServiceBusSender>();
        var receiver = provider.GetRequiredService<ServiceBusReceiver>();
        await provider.DisposeAsync();
        Assert.True(bus.IsClosed);
        Assert.True(sender.IsClosed);
        Assert.True(receiver.IsClosed);
    }

    [Fact]
    public async Task CustomEndpointIsCapturedAndExistingRegistrationsArePreserved()
    {
        AzlocalClientOptions? captured = null;
        var services = new ServiceCollection();
        var existing = new BlobServiceClient(new Uri("https://127.0.0.1:9999/existing"), new AzlocalCredential());
        services.AddSingleton(existing);
        services.AddAzLocal(options =>
        {
            captured = options;
            options.BaseUrl = "https://127.0.0.1:5001";
            options.BlobAccountName = "account";
            options.KeyVaultName = "vault";
        });
        captured!.BaseUrl = "https://127.0.0.1:9999";
        await using var provider = services.BuildServiceProvider();
        Assert.Same(existing, provider.GetRequiredService<BlobServiceClient>());
        Assert.Equal("https://127.0.0.1:5001/kv/vault", provider.GetRequiredService<SecretClient>().VaultUri.ToString());
    }

    [Theory]
    [InlineData("http://127.0.0.1:4566", 5672)]
    [InlineData("https://example.com", 5672)]
    [InlineData("https://127.0.0.1/path", 5672)]
    [InlineData("https://127.0.0.1:4566", 0)]
    public void InvalidConfigurationDoesNotLeavePartialRegistrations(string endpoint, int port)
    {
        var services = new ServiceCollection();
        Assert.ThrowsAny<ArgumentException>(() => services.AddAzLocal(options =>
        {
            options.BaseUrl = endpoint;
            options.AmqpPort = port;
        }));
        Assert.Empty(services);
    }

    private sealed class Consumer(BlobContainerClient blobs, SecretClient secrets, ServiceBusSender sender)
    {
        public BlobContainerClient Blobs { get; } = blobs;
        public SecretClient Secrets { get; } = secrets;
        public ServiceBusSender Sender { get; } = sender;
    }
}
