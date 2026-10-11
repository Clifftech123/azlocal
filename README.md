![azlocal logo](assets/branding/icon.png)

# azlocal

[![CI](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Clifftech123/azlocal/actions/workflows/ci.yml)
[![CLI version](https://img.shields.io/nuget/vpre/azlocal?label=CLI&color=0078D4)](https://www.nuget.org/packages/azlocal)
[![Client version](https://img.shields.io/nuget/vpre/AzLocal.Client?label=Client&color=0078D4)](https://www.nuget.org/packages/AzLocal.Client)
[![CLI downloads](https://img.shields.io/nuget/dt/azlocal?label=CLI%20downloads&color=0078D4)](https://www.nuget.org/packages/azlocal)
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

You need the .NET 10 SDK on Windows, Linux, or macOS. Check it with `dotnet --list-sdks`; an application runtime alone does not include the certificate setup tools. Source builds use the SDK selection in `global.json`.

The CLI runs the emulator. The client library connects your application to it. Version `0.1.0-alpha.1` is available on NuGet as a testing prerelease. Install it with:

```sh
dotnet tool install --global azlocal --version 0.1.0-alpha.1
```

From your application or test project directory:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

Check [GitHub releases](https://github.com/Clifftech123/azlocal/releases) for published versions and changes. The Core dependency is installed automatically with the client library. Use the [source setup](#contributing) if you want to work on the project or test unreleased changes.

**Windows Application Control:** the current alpha can be blocked before startup on systems enforcing Smart App Control. NuGet installation succeeding does not mean Windows permits execution. See [the known signing issue](docs/WINDOWS_APP_CONTROL.md).

Once installed, start the emulator:

```sh
azlocal trust-cert
azlocal start
azlocal wait
```

The host runs in the background at `https://127.0.0.1:4566`. `wait` checks that it responds before you run your application or tests. Certificate trust may require an extra step on your operating system or CI runner.

Use `127.0.0.1` when configuring Blob Storage clients so the SDK parses the local account paths correctly.

### Certificate trust

Run `azlocal trust-cert` once before starting the host, using the same user account. It calls `dotnet dev-certs https --trust`. Accept the operating system's trust prompt, then verify:

```sh
dotnet dev-certs https --check --trust
```

On Linux, use `dotnet dev-certs https --trust --verbose` if setup needs additional trust-store tools or environment settings. Follow the SDK's output. If you already have a certificate from an older SDK, it may need replacement to cover `127.0.0.1`.

See [HTTPS setup](docs/HTTPS.md) for Windows, macOS, Linux, WSL, and CI instructions. An untrusted certificate can make `wait` time out or `status` report `NOT running` even when the process has started.

### Custom ports

Stop the existing host before changing ports:

```sh
azlocal stop
azlocal start --port 5001
azlocal wait --port 5001 --timeout 60
azlocal status --port 5001
```

Set `options.BaseUrl = "https://127.0.0.1:5001"` in your application's `AddAzLocal` registration. Service Bus remains on `5672`: changing the HTTP port does not change AMQP. To use another AMQP port, set the host environment variable `AzLocal__AmqpPort` before starting it and match `options.AmqpPort` in the client. See [Configuration](docs/CONFIGURATION.md#sdk-endpoints).

### CLI commands

| Command | Purpose |
| --- | --- |
| `start [--port N]` | Start the host; the default port is 4566 |
| `wait [--port N] [--timeout S]` | Wait for the host to respond; the default timeout is 30 seconds |
| `status [--port N]` | Check whether the host responds |
| `stop` | Stop the host started by the CLI |
| `reset` | Stop the host and delete the default temporary state directory |
| `trust-cert` | Trust the ASP.NET Core development certificate |

The CLI tracks one background host per system temporary azlocal directory, including when you choose another port. Stop it before starting a replacement. For isolated parallel tests, use test fixtures with separate HTTP/AMQP ports and state directories.

### Update or uninstall

After a newer version is published, stop the host and select the same version for the tool and client package:

```sh
azlocal stop
dotnet tool update --global azlocal --version <published-version>
dotnet add package AzLocal.Client --version <published-version>
```

Replace `<published-version>` with the version you want from [Releases](https://github.com/Clifftech123/azlocal/releases), then restart the host. To remove the tool, run `azlocal stop` followed by `dotnet tool uninstall --global azlocal`. Uninstalling the tool does not remove its stored data or certificate trust.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| `azlocal` command not found | Confirm `dotnet tool list --global` includes it and your .NET global tools directory is on `PATH`; reopen the terminal after installation. |
| `An Application Control policy has blocked this file` | Windows blocked the executable before startup. Certificate trust will not fix it; see [Windows App Control](docs/WINDOWS_APP_CONTROL.md). |
| NuGet package or version not found | Confirm that version is published and indexed. Before the first release, use the source setup below. |
| Certificate error, `wait` timeout, or `NOT running` | Verify certificate trust and hostname coverage; check the matching HTTP port. See [HTTPS setup](docs/HTTPS.md). |
| Host exits immediately | Check HTTP and AMQP port conflicts. Run the source host in the foreground to see startup errors: `dotnet run --project src/AzLocal.Host -- --urls https://127.0.0.1:4566`. |
| Service Bus connection fails | Match the host and client AMQP ports, confirm AMQP is enabled, and check whether another process uses that port. |
| Injected client cannot be resolved | Set the resource names required for that client in `AddAzLocal`; register your application service too. |
| Data disappears after restart | `InMemory` loses metadata and messages. Use `Sqlite` or `JsonSnapshot` and persistent directories. |
| `reset` leaves custom data behind | It clears only the default temporary azlocal directory; custom directories need separate cleanup. |

For an issue report, include the azlocal version, OS, .NET SDK version, command or SDK operation, relevant configuration, and error output. Remove private values before sharing logs. Report it in [GitHub Issues](https://github.com/Clifftech123/azlocal/issues).

## Connect your application

With `AzLocal.Client` referenced in your application or test project, register the clients once with dependency injection. Application services receive the SDK clients through their constructors.

### Dependency injection

For an ASP.NET Core application, register the local clients in `Program.cs`:

```csharp
using AzLocal.Client;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BaseUrl = "https://127.0.0.1:4566";
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

Choose where the emulator stores resource metadata, secrets, and messages:

| State mode | Storage | Survives a host restart? |
| --- | --- | --- |
| `InMemory` (default) | Memory | No |
| `JsonSnapshot` | `<system-temp>/azlocal/state.json` | Yes, while the file exists |
| `Sqlite` | `<system-temp>/azlocal/state.db` | Yes, while the file exists |

Blob contents are stored separately in `<system-temp>/azlocal/blobs` in every mode. With `InMemory`, blob metadata is lost when the host stops, even though the files remain. Choose a persistent state mode to use those blobs after restarting.

The system temporary directory comes from `Path.GetTempPath()`: usually `%TEMP%` on Windows and `/tmp` on Linux. For data you want to keep, configure a directory outside system temporary storage.

### SQLite setup

SQLite runs inside the emulator. You do not need SQL Server, Docker, a connection string, or a SQL schema script. azlocal creates `state.db` and its table automatically.

Set the host's environment variables before starting it. These examples keep the database and blob files in a project-local `.azlocal` directory.

Windows PowerShell:

```powershell
azlocal stop
$stateDirectory = Join-Path (Get-Location) ".azlocal"
$env:AzLocal__StateMode = "Sqlite"
$env:AzLocal__SqlitePath = $stateDirectory
$env:AzLocal__BlobStorePath = Join-Path $stateDirectory "blobs"
azlocal start
azlocal wait
```

Linux or macOS:

```sh
azlocal stop
export AzLocal__StateMode=Sqlite
export AzLocal__SqlitePath="$PWD/.azlocal"
export AzLocal__BlobStorePath="$PWD/.azlocal/blobs"
azlocal start
azlocal wait
```

The directories are created automatically. These variables also apply when starting the host with `dotnet run`. Restart the host after changing its storage settings. `AddAzLocal` configures application clients; storage settings belong to the emulator host.

### Temporary state and other options

For SQLite in the default temporary directory, set only `AzLocal__StateMode=Sqlite` in a shell with no custom storage paths set. For a JSON snapshot, use `JsonSnapshot` and optionally set `AzLocal__SnapshotPath`. For disposable metadata and messages, use `InMemory`.

| Environment variable | Purpose |
| --- | --- |
| `AzLocal__StateMode` | `InMemory`, `JsonSnapshot`, or `Sqlite`; use this exact spelling |
| `AzLocal__SqlitePath` | Directory containing `state.db` |
| `AzLocal__SnapshotPath` | Directory containing `state.json` |
| `AzLocal__BlobStorePath` | Directory containing blob files |

Run one host per state directory. `azlocal reset` clears the default temporary state directory; it does not clear custom directories such as `.azlocal`. Exclude that directory from version control; this repository already does.

The blob storage format changed to separate accounts and preserve case-sensitive names. Blob data created with the previous format needs to be recreated; there is no automatic migration.

See [Configuration](docs/CONFIGURATION.md) for the host settings template and endpoint options.

azlocal accepts fake credentials and only allows local connections. Use test data and test secrets. Keep the emulator on your machine or inside your test environment.

## Work still to do

Contributions are welcome for the planned services in the [status table](#service-status). Existing services also need more coverage:

- Service Bus: add topics, sessions, transactions, scheduled delivery, message deferral, and administration APIs.
- Resource management: extend SDK support beyond subscriptions and resource groups.
- Blob Storage and Key Vault: extend API coverage and add SDK compatibility tests for additional operations.
- Storage: add a migration for blob data created with the previous format.
- CLI: check installation, certificate setup, startup, and shutdown on Windows, Linux, and macOS.

There are no release dates for these items yet.

## Contributing

Clone the repository if you want to work on azlocal or try it before the NuGet release:

```sh
git clone https://github.com/Clifftech123/azlocal.git
cd azlocal
dotnet build
dotnet test
```

To run the host directly from the checkout:

```sh
dotnet dev-certs https --trust
dotnet run --project src/AzLocal.Host -- --urls https://127.0.0.1:4566
```

Keep that process running while you use your application. To reference the client library before it is published, run this from your application or test project, replacing the path with your checkout location:

```sh
dotnet add reference /path/to/azlocal/src/AzLocal.Client/AzLocal.Client.csproj
```

To build and install the CLI locally, run these commands from the repository directory:

```powershell
dotnet restore
dotnet pack src/AzLocal.Cli -c Release -o artifacts/packages
$version = dotnet msbuild src/AzLocal.Cli -target:MinVer -getProperty:Version -nologo
dotnet tool install --global azlocal --add-source artifacts/packages --version $version
```

The tool package includes the host and can run from any directory. MinVer calculates the version from Git tags; untagged builds get a development prerelease version.

See [Contributing](docs/CONTRIBUTING.md) for the project layout and [Services](docs/SERVICES.md) for service documentation.

See [Releasing](docs/RELEASING.md) for prerelease testing, NuGet setup, and stable publication.

## License

[MIT](LICENSE)
