namespace AzLocal.Client;

/// <summary>
/// Creates pre-configured <see cref="HttpClient"/> instances and exposes the
/// <see cref="AzlocalCredential"/> for use with Azure SDK clients in local development.
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

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with its base address set to the emulator URL,
    /// and a pre-set <c>Authorization</c> header carrying the fake bearer token.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fake-azlocal-token");
        return client;
    }

    /// <summary>Returns the blob storage base URL for the given account.</summary>
    public Uri GetBlobEndpoint(string account) => new($"{_baseUrl}/azu/{account}");
}
