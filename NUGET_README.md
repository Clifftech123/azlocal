![azlocal logo](https://raw.githubusercontent.com/Clifftech123/azlocal/main/assets/branding/icon.png)

# azlocal

[![CI](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/azlocal?label=NuGet&color=0078D4)](https://www.nuget.org/packages/azlocal)
[![Downloads](https://img.shields.io/nuget/dt/azlocal?color=0078D4)](https://www.nuget.org/packages/azlocal)
[![License: MIT](https://img.shields.io/badge/license-MIT-0078D4)](https://github.com/Clifftech123/azlocal/blob/main/LICENSE)

**Run Azure services locally with the official .NET SDKs.**

Test blob uploads, secrets, message queues, and resource groups on your machine. No Azure subscription or cloud credentials are needed. Requires the **.NET 10 SDK**.

## Quick start

Install the emulator and start it:

```sh
dotnet tool install --global azlocal --version 0.1.0-alpha.1
azlocal trust-cert
azlocal start
azlocal wait
```

| Endpoint | Address |
| --- | --- |
| HTTPS services | `https://127.0.0.1:4566` |
| Service Bus AMQP | `127.0.0.1:5672` |

`azlocal status` checks the host. `azlocal stop` stops it. Version `0.1.0-alpha.1` is a testing prerelease; see [release notes](https://github.com/Clifftech123/azlocal/releases) for updates.

**Certificate setup:** approve the trust prompt, then run `dotnet dev-certs https --check --trust`. On Linux, use `dotnet dev-certs https --trust --verbose` and follow its instructions. Older certificates may not cover `127.0.0.1`. See the [HTTPS guide](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md) for platform and CI setup.

## Use it in your application

Add the client library to your application project:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

Register only the resources your application uses. For example, in an ASP.NET Core app's `Program.cs`:

```csharp
using AzLocal.Client;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
});

var app = builder.Build();
app.MapPost("/uploads/{name}", async (
    string name, HttpRequest request, BlobContainerClient container,
    CancellationToken cancellationToken) =>
{
    await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    await container.GetBlobClient(name).UploadAsync(
        request.Body, overwrite: true, cancellationToken: cancellationToken);
    return Results.Ok();
});
app.Run();
```

Clients are singletons and can also be injected into scoped or transient application services. The emulator must be running before you make requests.

Set `KeyVaultName` to inject `SecretClient`, or `ServiceBusQueueName` to inject `ServiceBusSender` and `ServiceBusReceiver`. For custom ports, match `BaseUrl` and `AmqpPort` to the host settings. See [DI examples](https://github.com/Clifftech123/azlocal#connect-your-application).

## Supported services

| Service | Local operations |
| --- | --- |
| Blob Storage | Containers; upload, download, list, delete, properties |
| Key Vault Secrets | Set, get, list, delete, versions |
| Service Bus queues | AMQP sends and batches, peek, receive, settlement, lock renewal, dead-letter queues |
| Resource management | Resource groups and subscription discovery |
| Managed identity | Local test-token acquisition |

Support covers these operations rather than the complete Azure APIs. Managed identity returns test tokens. [SDK compatibility](https://github.com/Clifftech123/azlocal/blob/main/docs/SDK_COMPAT.md) lists the limits.

## Choose your package

| Package | Purpose |
| --- | --- |
| **azlocal** | CLI with the bundled emulator host |
| **AzLocal.Client** | DI registration for local Azure SDK clients |
| **AzLocal.Core** | Shared dependency, installed automatically with the client |

State can use memory, JSON snapshots, or SQLite. Use a persistent state mode to retain metadata and messages after restart. [Configuration](https://github.com/Clifftech123/azlocal/blob/main/docs/CONFIGURATION.md) covers storage and ports; [troubleshooting](https://github.com/Clifftech123/azlocal#troubleshooting) covers startup and certificate errors.

**[Documentation](https://github.com/Clifftech123/azlocal#readme) · [Release notes](https://github.com/Clifftech123/azlocal/releases) · [Report an issue](https://github.com/Clifftech123/azlocal/issues)**

MIT licensed. An independent project for local development and testing, not affiliated with Microsoft.
