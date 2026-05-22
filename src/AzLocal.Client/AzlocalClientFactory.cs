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
    private const string DefaultBaseUrl = "http://localhost:4566";

    /// <summary>The credential that satisfies Azure SDK authentication against the local emulator.</summary>
    public AzlocalCredential Credential { get; } = new();

    public AzlocalClientFactory(string baseUrl = DefaultBaseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
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
        var uri = new Uri($"{_baseUrl}/azu/{account}/{container}");
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
        var uri = new Uri($"{_baseUrl}/azu/{account}/{container}/{blobName}");
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
            Retry = { MaxRetries = 0 }
        };
        return new SecretClient(uri, Credential, options);
    }

    // -------------------------------------------------------------------------
    // Service Bus
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a pre-configured <see cref="HttpClient"/> for the Service Bus emulator.
    /// The Azure Service Bus SDK uses AMQP (not HTTP), so direct SDK integration is not
    /// supported. Use this client to call the emulator's HTTP queue endpoints directly.
    /// <code>
    /// var sb = factory.CreateServiceBusHttpClient("mynamespace");
    /// await sb.PostAsync("myqueue/messages", new StringContent("hello"));
    /// </code>
    /// </summary>
    public HttpClient CreateServiceBusHttpClient(string @namespace)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"{_baseUrl}/sb/{@namespace}/")
        };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fake-azlocal-token");
        return client;
    }

    // -------------------------------------------------------------------------
    // Raw HTTP (ARM or any other emulated service)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a general-purpose <see cref="HttpClient"/> with the emulator base address
    /// and auth header set. Use this for ARM calls or any service not covered above.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fake-azlocal-token");
        return client;
    }

    /// <summary>Returns the blob storage base URI for the given account.</summary>
    public Uri GetBlobEndpoint(string account) => new($"{_baseUrl}/azu/{account}");

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static BlobClientOptions BuildBlobOptions() => new()
    {
        // Disable retries so tests fail fast instead of hanging on connection errors.
        Retry = { MaxRetries = 0 }
    };
}
