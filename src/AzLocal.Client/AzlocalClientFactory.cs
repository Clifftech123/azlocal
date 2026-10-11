using AzLocal.Core;
using Azure.Core.Pipeline;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AzLocal.Client;

/// <summary>
/// One-stop factory for creating Azure SDK clients pre-configured to talk to the
/// local AzLocal emulator. Pass <c>new AzlocalClientFactory()</c> and call the
/// method for the service you need — no connection strings, no Azure tenant required.
/// </summary>
public sealed class AzlocalClientFactory
{
    private readonly string _baseUrl;
    private readonly HttpMessageHandler? _handler;
    private const string DefaultBaseUrl = EmulatorDefaults.BaseUrl;

    /// <summary>The credential that satisfies Azure SDK authentication against the local emulator.</summary>
    public AzlocalCredential Credential { get; } = new();

    /// <param name="baseUrl">The emulator's base address.</param>
    /// <param name="handler">
    /// Optional transport handler. Pass the handler from an in-process test host (e.g.
    /// <c>WebApplicationFactory.Server.CreateHandler()</c>) to route every client created by
    /// this factory through that host in-memory instead of over a real socket connection.
    /// </param>
    public AzlocalClientFactory(string baseUrl = DefaultBaseUrl, HttpMessageHandler? handler = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _handler = handler;
    }

    // -------------------------------------------------------------------------
    // Blob Storage
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="BlobServiceClient"/> pointed at the emulator for the given
    /// storage account. Use this to manage containers and list blobs.
    /// <code>
    /// var blobs = factory.CreateBlobServiceClient("myaccount");
    /// await blobs.CreateBlobContainerAsync("uploads");
    /// </code>
    /// </summary>
    public BlobServiceClient CreateBlobServiceClient(string account)
    {
        var options = BuildBlobOptions();
        return new BlobServiceClient(GetBlobEndpoint(account), Credential, options);
    }

    /// <summary>
    /// Returns a <see cref="BlobContainerClient"/> pointed at a specific container.
    /// <code>
    /// var container = factory.CreateBlobContainerClient("myaccount", "uploads");
    /// await container.UploadBlobAsync("file.txt", stream);
    /// </code>
    /// </summary>
    public BlobContainerClient CreateBlobContainerClient(string account, string container)
    {
        var uri = new Uri($"{_baseUrl}/{account}/{container}");
        return new BlobContainerClient(uri, Credential, BuildBlobOptions());
    }

    /// <summary>
    /// Returns a <see cref="BlobClient"/> for a specific blob.
    /// <code>
    /// var blob = factory.CreateBlobClient("myaccount", "uploads", "folder/file.txt");
    /// await blob.UploadAsync(stream, overwrite: true);
    /// </code>
    /// </summary>
    public BlobClient CreateBlobClient(string account, string container, string blobName)
    {
        var uri = new Uri($"{_baseUrl}/{account}/{container}/{blobName}");
        return new BlobClient(uri, Credential, BuildBlobOptions());
    }

    // -------------------------------------------------------------------------
    // Key Vault
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="SecretClient"/> pointed at the emulator for the given vault.
    /// <code>
    /// var secrets = factory.CreateSecretClient("myvault");
    /// await secrets.SetSecretAsync("my-api-key", "super-secret");
    /// var value  = await secrets.GetSecretAsync("my-api-key");
    /// </code>
    /// </summary>
    public SecretClient CreateSecretClient(string vault)
    {
        var uri = new Uri($"{_baseUrl}/kv/{vault}");
        var options = new SecretClientOptions
        {
            Retry = { MaxRetries = 0 },
            // The SDK's challenge-response auth refuses to send a token when the challenge's
            // "resource" domain doesn't match the request host — which is always true for a
            // local emulator, since it can never live at *.vault.azure.net.
            DisableChallengeResourceVerification = true
        };
        if (_handler is not null)
            options.Transport = new HttpClientTransport(_handler);
        return new SecretClient(uri, Credential, options);
    }

    // -------------------------------------------------------------------------
    // Service Bus
    // -------------------------------------------------------------------------

    /// <summary>Creates an official Service Bus client for the local AMQP listener.</summary>
    /// <remarks>AMQP queues share the HTTP namespace "local". The listener is loopback-only.</remarks>
    public Azure.Messaging.ServiceBus.ServiceBusClient CreateServiceBusClient(int amqpPort = 5672)
    {
        if (amqpPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(amqpPort));
        var address = new Uri(_baseUrl);
        if (!System.Net.IPAddress.TryParse(address.Host, out var ip) || !System.Net.IPAddress.IsLoopback(ip))
            throw new InvalidOperationException("The AMQP client requires a loopback IP address.");
        return new Azure.Messaging.ServiceBus.ServiceBusClient(
            $"Endpoint=sb://{address.Host}:{amqpPort};SharedAccessKeyName=local;SharedAccessKey=local;UseDevelopmentEmulator=true;",
            new Azure.Messaging.ServiceBus.ServiceBusClientOptions
            {
                RetryOptions = new Azure.Messaging.ServiceBus.ServiceBusRetryOptions { MaxRetries = 0, TryTimeout = TimeSpan.FromSeconds(10) }
            });
    }

    /// <summary>
    /// Returns a pre-configured <see cref="HttpClient"/> for the Service Bus emulator.
    /// Use this client for HTTP queue endpoints, or CreateServiceBusClient for AMQP.
    /// <code>
    /// var sb = factory.CreateServiceBusHttpClient("mynamespace");
    /// await sb.PostAsync("myqueue/messages", new StringContent("hello"));
    /// </code>
    /// </summary>
    public HttpClient CreateServiceBusHttpClient(string @namespace)
    {
        var client = CreateRawHttpClient();
        client.BaseAddress = new Uri($"{_baseUrl}/sb/{@namespace}/");
        return client;
    }

    // -------------------------------------------------------------------------
    // Raw HTTP (ARM or any other emulated service)
    // -------------------------------------------------------------------------

    /// <summary>Creates an official ARM client for local resource groups and subscriptions.</summary>
    public Azure.ResourceManager.ArmClient CreateArmClient(string? subscriptionId = null, Azure.Core.TokenCredential? credential = null)
    {
        var options = new Azure.ResourceManager.ArmClientOptions
        {
            Environment = new Azure.ResourceManager.ArmEnvironment(new Uri(_baseUrl), "https://management.azure.com/"),
            Retry = { MaxRetries = 0 }
        };
        if (_handler is not null) options.Transport = new HttpClientTransport(_handler);
        return new Azure.ResourceManager.ArmClient(credential ?? Credential, subscriptionId, options);
    }

    /// <summary>
    /// Returns a general-purpose <see cref="HttpClient"/> with the emulator base address
    /// and auth header set. Use this for ARM calls or any service not covered above.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        var client = CreateRawHttpClient();
        client.BaseAddress = new Uri(_baseUrl);
        return client;
    }

    /// <summary>Returns the blob storage base URI for the given account.</summary>
    public Uri GetBlobEndpoint(string account) => new($"{_baseUrl}/{account}");

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Creates an official managed identity credential pointed at the local IMDS endpoint.</summary>
    public Azure.Identity.ManagedIdentityCredential CreateManagedIdentityCredential()
    {
        var uri = new Uri(_baseUrl);
        if (!(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            System.Net.IPAddress.TryParse(uri.Host, out var address) && System.Net.IPAddress.IsLoopback(address)))
            throw new InvalidOperationException("Local managed identity requires a loopback endpoint.");
        var options = new Azure.Identity.ManagedIdentityCredentialOptions { Retry = { MaxRetries = 0 } };
        // Azure Identity clones credential options when probing IMDS. Route at the
        // transport boundary so both probes and token requests stay local.
        options.Transport = new LocalIdentityTransport(
            _handler is null ? HttpClientTransport.Shared : new HttpClientTransport(_handler), uri);
        return new Azure.Identity.ManagedIdentityCredential(options);
    }

    private sealed class LocalIdentityTransport(HttpPipelineTransport inner, Uri endpoint) : HttpPipelineTransport
    {
        public override Azure.Core.Request CreateRequest() => inner.CreateRequest();

        public override void Process(Azure.Core.HttpMessage message)
        {
            Route(message);
            inner.Process(message);
        }

        public override ValueTask ProcessAsync(Azure.Core.HttpMessage message)
        {
            Route(message);
            return inner.ProcessAsync(message);
        }

        private void Route(Azure.Core.HttpMessage message)
        {
            var path = message.Request.Uri.ToUri().PathAndQuery;
            if (!path.StartsWith("/metadata/", StringComparison.Ordinal))
                throw new InvalidOperationException("Only IMDS requests are supported by the local identity credential.");
            message.Request.Uri.Reset(new Uri(endpoint, path));
        }
    }

    private BlobClientOptions BuildBlobOptions()
    {
        var options = new BlobClientOptions
        {
            // Disable retries so tests fail fast instead of hanging on connection errors.
            Retry = { MaxRetries = 0 }
        };
        if (_handler is not null)
            options.Transport = new HttpClientTransport(_handler);
        return options;
    }

    // Handler is owned by the caller (e.g. WebApplicationFactory) when supplied, so it must
    // not be disposed alongside the HttpClient.
    private HttpClient CreateRawHttpClient()
    {
        var client = _handler is not null
            ? new HttpClient(_handler, disposeHandler: false)
            : new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AzlocalCredential.TokenValue);
        return client;
    }
}
