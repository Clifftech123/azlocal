![azlocal — Azure services. Local development.](https://raw.githubusercontent.com/Clifftech123/azlocal/main/assets/branding/banner.png)

# azlocal

[![CI](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml)
[![NuGet: release pending](https://img.shields.io/badge/NuGet-release%20pending-0078D4)](https://github.com/Clifftech123/azlocal/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-0078D4)](https://github.com/Clifftech123/azlocal/blob/main/LICENSE)

Run a local Azure emulator for .NET development and automated tests. Work with blobs, test secrets, queues, and resource groups through the official Azure SDK clients, without an Azure subscription.

## Packages

| Package | Use |
| --- | --- |
| `azlocal` | Command-line tool and bundled emulator host |
| `AzLocal.Client` | Local Azure SDK clients registered through dependency injection |
| `AzLocal.Core` | Shared dependency; installed automatically with the client |

You need the .NET 10 SDK. The first planned testing release is `0.1.0-alpha.1`. These commands work after that version is published.

## Start the emulator

```sh
dotnet tool install --global azlocal --version 0.1.0-alpha.1
azlocal trust-cert
azlocal start
azlocal wait
```

The HTTPS endpoint is `https://127.0.0.1:4566`; Service Bus uses local AMQP on port `5672`. Certificate trust depends on your operating system. Use `azlocal status` to check the host and `azlocal stop` to stop it.

### Certificate setup

`azlocal trust-cert` runs `dotnet dev-certs https --trust`. Run it as the same user who starts the emulator and approve the trust prompt when requested. Verify with:

```sh
dotnet dev-certs https --check --trust
```

On Linux, run `dotnet dev-certs https --trust --verbose` and follow its trust-store instructions. Older certificates may not cover `127.0.0.1`. A trust problem can cause `wait` to time out even when the host process is running. See the [platform and CI HTTPS guide](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md).

To change the HTTPS port, stop the host, run `azlocal start --port 5001`, and use `azlocal wait --port 5001`. Match that address in `options.BaseUrl`. The AMQP port is configured separately; see [host configuration](https://github.com/Clifftech123/azlocal/blob/main/docs/CONFIGURATION.md).

## Connect your application

From your application project:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

Register clients in an ASP.NET Core application's `Program.cs`:

```csharp
using AzLocal.Client;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BaseUrl = "https://127.0.0.1:4566";
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
    options.KeyVaultName = "myvault";
    options.ServiceBusQueueName = "myqueue";
});
builder.Services.AddScoped<UploadService>();

var app = builder.Build();
app.MapPost("/uploads/{name}", async (
    string name, HttpRequest request, UploadService uploads,
    CancellationToken cancellationToken) =>
{
    await uploads.UploadAsync(name, request.Body, cancellationToken);
    return Results.Ok();
});
app.Run();

public sealed class UploadService(BlobContainerClient container)
{
    public async Task UploadAsync(
        string name, Stream content, CancellationToken cancellationToken)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await container.GetBlobClient(name).UploadAsync(
            content, overwrite: true, cancellationToken: cancellationToken);
    }
}
```

SDK clients are singletons. Application services can be scoped or transient. The emulator must already be running; registration does not start the host.

## Available services

| Service | Supported local operations |
| --- | --- |
| Blob Storage | Container creation and deletion; blob upload, download, listing, properties, and deletion |
| Key Vault Secrets | Set, get, list, and delete secrets; read secret versions |
| Service Bus queues | AMQP single and batch sends, peek, receive, complete, abandon, lock renewal, and dead-letter queues |
| Resource management | Create, read, list, and delete resource groups; read the configured subscription |
| Managed identity | Acquire local test tokens through the registered `ManagedIdentityCredential` |

These operations cover part of the Azure APIs. Managed identity returns test tokens. State can use memory, JSON snapshots, or SQLite. Old development blob data must be recreated after storage format changes.

## Documentation and feedback

- [Configuration, examples, and storage setup](https://github.com/Clifftech123/azlocal#readme)
- [SDK compatibility and limitations](https://github.com/Clifftech123/azlocal/blob/main/docs/SDK_COMPAT.md)
- [Report an issue](https://github.com/Clifftech123/azlocal/issues)
- [Release notes](https://github.com/Clifftech123/azlocal/releases)
- [Setup troubleshooting and upgrades](https://github.com/Clifftech123/azlocal#troubleshooting)

azlocal is an independent project for local development and testing. It is not affiliated with Microsoft. Licensed under MIT.
