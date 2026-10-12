![azlocal logo](assets/branding/icon.png)

# azlocal

[![CI](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml)
[![Client version](https://img.shields.io/nuget/vpre/AzLocal.Client?label=Client&color=0078D4)](https://www.nuget.org/packages/AzLocal.Client)
[![Client downloads](https://img.shields.io/nuget/dt/AzLocal.Client?label=Client%20downloads&color=0078D4)](https://www.nuget.org/packages/AzLocal.Client)
[![License: MIT](https://img.shields.io/badge/license-MIT-0078D4)](LICENSE)

Run Azure services locally while you build and test .NET applications.

azlocal runs a local server for a small set of Azure APIs. Use it to test blob uploads, work with test secrets, and exercise queue operations without an Azure subscription. Blob Storage, Key Vault, Service Bus queues, and resource groups work with the official Azure SDK clients. HTTP endpoints are also available.

The project is under active development. It is built for local development and automated tests, with more Azure services planned.

## Service status

The goal is to support the services below for local development and testing. Some work today; others are on the roadmap. Support is added and tested one set of operations at a time.

### Available now

| Service | Tested local operations | Client |
| --- | --- | --- |
| Blob Storage | Create and delete containers; upload, download, list, and delete blobs; read blob properties | `Azure.Storage.Blobs` |
| Key Vault Secrets | Set, get, list, and delete secrets; read secret versions | `Azure.Security.KeyVault.Secrets` |
| Service Bus queues | Send single messages and batches; peek, receive, complete, abandon, renew locks, and dead-letter messages; read dead-letter queues | `Azure.Messaging.ServiceBus` over local AMQP, or HTTP |
| Resource management | Create, read, list, and delete resource groups; preserve tags; read the configured subscription | `Azure.ResourceManager`, or HTTP |
| Managed identity | Acquire a local test token with resource and expiry fields | `Azure.Identity.ManagedIdentityCredential` through DI |

These services support the operations listed here. They do not implement the full Azure APIs. Managed identity returns test tokens. See [SDK compatibility](docs/SDK_COMPAT.md) for setup and remaining limitations.

### Planned support

These services are not implemented yet. The scope below describes what we intend to build first. Contributions are welcome.

| Service | Status | Initial scope |
| --- | --- | --- |
| Queue Storage | Planned | Create and delete queues; send, peek, receive, update, and delete messages; visibility timeouts |
| Table Storage | Planned | Create and delete tables; insert, read, update, and delete entities; basic queries |
| App Configuration | Planned | Read and write configuration values; labels and feature flags |
| Event Grid | Planned | Publish events and deliver them to local HTTP subscribers |
| Cosmos DB for NoSQL | Planned | Create databases and containers; create, read, update, and delete documents; basic queries |
| Azure Functions (HTTP triggers) | Planned | Integrate a local Functions runtime with the emulated services |

The suggested build order is Queue Storage, Table Storage, App Configuration, then Event Grid. Cosmos DB and Functions integration need more design work. There are no release dates yet.

## Install and run

Use Docker Desktop with Linux containers on Windows/macOS, or Docker Engine and Compose on Linux. From a checkout containing Docker support:

```sh
docker compose up --build --detach --wait azlocal
docker compose ps
```

The emulator exposes HTTPS at `https://127.0.0.1:4566` and AMQP at `127.0.0.1:5672`. SQLite and blob contents are stored in a named volume.

The container generates its own HTTPS certificate. Export the public certificate for your application's trust store or local-development pinning:

```sh
docker compose cp azlocal:/certs/localhost.crt ./azlocal-localhost.crt
```

Place that public certificate in your application's working directory for the DI example below. See [Docker setup](docs/DOCKER.md) for containerized tests, certificate handling, storage, ports, and using published Docker Hub images.

```sh
docker compose logs azlocal
docker compose down
```

Stopping with `down` preserves data. Adding `--volumes` deletes this project's stored data and certificate.

Docker Hub images are published from releases containing Docker support; the older NuGet-only alpha has no image. Select a published tag from [Docker Hub](https://hub.docker.com/r/clifftech123/azlocal/tags) and use `compose.hub.yaml` as described in the Docker guide.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Container exits or is unhealthy | Run `docker compose logs azlocal`; check port conflicts and volume permissions. |
| HTTPS SDK requests fail | Trust or pin the exported container certificate. See [Docker HTTPS setup](docs/DOCKER.md#https-and-application-clients). |
| Service Bus cannot connect | Match the published AMQP port to the client; shared-network containers use internal port 5672. |
| Client cannot be resolved from DI | Set the resource names for the client you need and register your application service. |
| Data disappears | Keep the named volume and use a persistent state mode; avoid `down --volumes` when keeping data. |
| Windows blocks an application DLL | Dockerizing the server does not change Windows application policies. See [containerized applications/tests](docs/DOCKER.md#applications-and-tests-inside-docker). |

Report reproducible problems in [GitHub Issues](https://github.com/Clifftech123/azlocal/issues), including the image version, OS, Docker version, relevant configuration, and error output. Remove private values from logs.

## Connect your application

Add the client library to your .NET 10 application or test project:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

Core is installed automatically. Register clients once with DI; application services receive them through constructors. The server runs separately in Docker.

### Dependency injection

For an ASP.NET Core application, register the local clients in `Program.cs`:

```csharp
using AzLocal.Client;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using System.Security.Cryptography.X509Certificates;

using var certificate = X509CertificateLoader.LoadCertificateFromFile("azlocal-localhost.crt");
var thumbprint = certificate.Thumbprint;
using var transport = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, peer, _, _) => peer?.Thumbprint == thumbprint
};
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BaseUrl = "https://127.0.0.1:4566";
    options.TransportHandler = transport;
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
    options.KeyVaultName = "myvault";
    options.ServiceBusQueueName = "myqueue";
    options.AmqpPort = 5672;
});
builder.Services.AddScoped<UploadService>();

var app = builder.Build();
app.MapPost("/uploads/{name}", async (
    string name, HttpRequest request, UploadService uploads, CancellationToken cancellationToken) =>
{
    await uploads.UploadAsync(name, request.Body, cancellationToken);
    return Results.Ok();
});
app.Run();

public sealed class UploadService(BlobContainerClient container, ServiceBusSender sender)
{
    public async Task UploadAsync(string name, Stream content, CancellationToken cancellationToken)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await container.GetBlobClient(name).UploadAsync(
            content, overwrite: true, cancellationToken: cancellationToken);
        await sender.SendMessageAsync(new ServiceBusMessage(name), cancellationToken);
    }
}
```

`AddAzLocal` registers SDK clients as singletons so requests can reuse them. Your application services can be scoped, as above, or transient with `AddTransient`. The container closes registered Service Bus clients, senders, and receivers when the application shuts down; do not dispose injected clients yourself.

| Configuration | Clients available for injection |
| --- | --- |
| Default registration | `TokenCredential`, `ManagedIdentityCredential`, `ArmClient`, `ServiceBusClient` |
| `BlobAccountName` | `BlobServiceClient` |
| `BlobAccountName` and `BlobContainerName` | `BlobContainerClient` |
| `KeyVaultName` | `SecretClient` |
| `ServiceBusQueueName` | `ServiceBusSender` and `ServiceBusReceiver` |

A custom HTTP port is configured through `BaseUrl`; the AMQP port is separate. For in-process tests, supply `TransportHandler` from your test host. This HTTP handler does not replace the AMQP TCP connection.

Existing registrations are preserved. Register `AddAzLocal` once in your local development or test setup. The emulator still needs to be running; registering clients does not start it or create resources.

### Blob Storage

Inject `BlobContainerClient` into your application service:

```csharp
using Azure.Storage.Blobs;

public sealed class BlobExample(BlobContainerClient container)
{
    public async Task<string> UploadAndDownloadAsync(CancellationToken cancellationToken)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient("hello.txt");
        await blob.UploadAsync(new BinaryData("hello from azlocal"),
            overwrite: true, cancellationToken: cancellationToken);
        var download = await blob.DownloadContentAsync(cancellationToken);
        return download.Value.Content.ToString();
    }
}
```

### Key Vault Secrets

```csharp
using Azure.Security.KeyVault.Secrets;

public sealed class SecretExample(SecretClient secrets)
{
    public async Task<string> SetAndGetAsync(CancellationToken cancellationToken)
    {
        await secrets.SetSecretAsync("api-key", "test-value", cancellationToken);
        var secret = await secrets.GetSecretAsync("api-key", cancellationToken: cancellationToken);
        return secret.Value.Value;
    }
}
```

### Service Bus queues

The AMQP listener runs on `127.0.0.1:5672` and uses the namespace `local`. Set `ServiceBusQueueName` during registration to inject a sender and receiver for that queue. Queues are created when first used. Message locks last 30 seconds and can be renewed.

```csharp
using Azure.Messaging.ServiceBus;

public sealed class QueueExample(ServiceBusSender sender, ServiceBusReceiver receiver)
{
    public async Task<string?> SendAndReceiveAsync(CancellationToken cancellationToken)
    {
        await sender.SendMessageAsync(new ServiceBusMessage("hello"), cancellationToken);
        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
        if (message is null) return null;
        await receiver.CompleteMessageAsync(message, cancellationToken);
        return message.Body.ToString();
    }
}
```

Topics, sessions, transactions, scheduled delivery, message deferral, and administration APIs are not implemented.

### Resource groups

```csharp
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

public sealed class ResourceGroupExample(ArmClient arm)
{
    public async Task CreateAndDeleteAsync(CancellationToken cancellationToken)
    {
        var subscription = await arm.GetDefaultSubscriptionAsync(cancellationToken);
        var created = await subscription.GetResourceGroups().CreateOrUpdateAsync(
            WaitUntil.Completed, "my-rg", new ResourceGroupData(new AzureLocation("eastus")),
            cancellationToken);
        await created.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
    }
}
```

Register each application service you use with `AddScoped` or `AddTransient`, just like `UploadService` above.

### Managed identity

`AddAzLocal` registers `ManagedIdentityCredential` for local IMDS token acquisition. To use it as the credential for injected ARM clients, add this registration before `AddAzLocal`:

```csharp
using Azure.Core;
using Azure.Identity;

builder.Services.AddSingleton<TokenCredential>(provider =>
    provider.GetRequiredService<ManagedIdentityCredential>());
builder.Services.AddAzLocal();
```

Your application service still injects `ArmClient`. The endpoint validates token requests and returns test tokens with a one-hour expiry. It does not contact Microsoft Entra ID. IMDSv2 key-bound tokens are not supported.

## Use in tests

Start the emulator before your test run and register clients with `AddAzLocal` in the test service collection. Give each test unique resource names to keep test data separate.

For tests that run the host inside the test process, use `WebApplicationFactory<Program>` with a reference to `AzLocal.Host` and `Microsoft.AspNetCore.Mvc.Testing`. Set `options.TransportHandler` to `Server.CreateHandler()` in `AddAzLocal` and configure a separate blob directory for each fixture. The [dependency injection tests](tests/AzLocal.CompatTests/DependencyInjectionTests.cs) show registration and constructor injection against the test host. Service Bus still needs a local AMQP listener.

## State and configuration

The Docker image defaults to SQLite at `/data/state.db`, with blob files at `/data/blobs`. Compose mounts a named volume at `/data`, so data survives container recreation.

| Mode | Behavior |
| --- | --- |
| `Sqlite` (default) | Metadata, secrets, and messages persist in SQLite |
| `JsonSnapshot` | Metadata, secrets, and messages persist in a JSON snapshot |
| `InMemory` | Metadata and messages are discarded when the process stops |

Blob contents are files in every mode. Use a persistent metadata store to access existing blobs after a restart. One server should own each data volume.

Configure the server through Compose environment settings. For example, add this to the `azlocal` service to use JSON:

```yaml
environment:
  AzLocal__StateMode: JsonSnapshot
  AzLocal__SnapshotPath: /data
```

The image also sets the SQLite and blob paths under `/data`. See [Configuration](docs/CONFIGURATION.md) for the available settings. Changing modes does not migrate existing metadata. Older development blob formats must be recreated.

## Work still to do

Contributions are welcome for the planned services in the [status table](#service-status). Existing services also need more coverage:

- Service Bus: add topics, sessions, transactions, scheduled delivery, message deferral, and administration APIs.
- Resource management: extend SDK support beyond subscriptions and resource groups.
- Blob Storage and Key Vault: extend API coverage and add SDK compatibility tests for additional operations.
- Storage: add a migration for blob data created with the previous format.
- Docker: expand architecture coverage and containerized application examples.

There are no release dates for these items yet.

## Contributing

Clone the repository, then build and test with the .NET 10 SDK:

```sh
git clone https://github.com/Clifftech123/azlocal.git
cd azlocal
dotnet build
dotnet test
```

Run the server and containerized SDK checks with Docker:

```sh
docker compose up --build --detach --wait azlocal
docker compose --profile checks build checks
docker compose --profile checks run --rm checks
```

See [Contributing](docs/CONTRIBUTING.md) for the project layout, [Services](docs/SERVICES.md) for service documentation, and [Releasing](docs/RELEASING.md) for publishing.

## License

[MIT](LICENSE)
