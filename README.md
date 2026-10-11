# azlocal

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
| Managed identity | Acquire a local test token with resource and expiry fields | `Azure.Identity.ManagedIdentityCredential` through the factory |

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

You need the .NET 10 SDK.

The CLI runs the emulator. The client library connects your application to it. After the packages are published to NuGet, install them with:

```sh
dotnet tool install --global azlocal
```

From your application or test project directory:

```sh
dotnet add package AzLocal.Client
```

**Publication status:** these packages are not available on NuGet yet. Until the first release, use the [source setup](#contributing) below.

Once installed, start the emulator:

```sh
azlocal trust-cert
azlocal start
azlocal wait
```

The host runs in the background at `https://127.0.0.1:4566`. `wait` checks that it responds before you run your application or tests. Certificate trust may require an extra step on your operating system or CI runner.

Use `127.0.0.1` when configuring Blob Storage clients so the SDK parses the local account paths correctly.

### CLI commands

| Command | Purpose |
| --- | --- |
| `start [--port N]` | Start the host; the default port is 4566 |
| `wait [--port N] [--timeout S]` | Wait for the host to respond; the default timeout is 30 seconds |
| `status [--port N]` | Check whether the host responds |
| `stop` | Stop the host started by the CLI |
| `reset` | Stop the host and delete the default temporary state directory |
| `trust-cert` | Trust the ASP.NET Core development certificate |

## Connect your application

With `AzLocal.Client` referenced in your application or test project, create a factory for the running emulator:

```csharp
using AzLocal.Client;

var factory = new AzlocalClientFactory();
```

For a custom port, pass the address: `new AzlocalClientFactory("https://127.0.0.1:5001")`.

### Blob Storage

```csharp
var container = factory.CreateBlobContainerClient("myaccount", "uploads");
await container.CreateIfNotExistsAsync();

var blob = container.GetBlobClient("hello.txt");
await blob.UploadAsync(new BinaryData("hello from azlocal"), overwrite: true);

var download = await blob.DownloadContentAsync();
Console.WriteLine(download.Value.Content.ToString());
```

### Key Vault Secrets

```csharp
var secrets = factory.CreateSecretClient("myvault");

await secrets.SetSecretAsync("api-key", "test-value");
var secret = await secrets.GetSecretAsync("api-key");
Console.WriteLine(secret.Value.Value);
```

### Service Bus queues

The AMQP listener runs on `127.0.0.1:5672`. It uses the HTTP namespace `local`, so SDK clients and `CreateServiceBusHttpClient("local")` share the same queues. Queues are created when first used. Message locks last 30 seconds and can be renewed.

```csharp
using Azure.Messaging.ServiceBus;

await using var bus = factory.CreateServiceBusClient();
await using var sender = bus.CreateSender("myqueue");
await sender.SendMessageAsync(new ServiceBusMessage("hello"));

await using var receiver = bus.CreateReceiver("myqueue");
var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
if (message is not null)
{
    Console.WriteLine(message.Body.ToString());
    await receiver.CompleteMessageAsync(message);
}
```

This supports local queue operations. Topics, sessions, transactions, scheduled delivery, message deferral, and administration APIs are not implemented.

### Resource groups

```csharp
using Azure;
using Azure.Core;
using Azure.ResourceManager.Resources;

var arm = factory.CreateArmClient();
var subscription = await arm.GetDefaultSubscriptionAsync();
var groups = subscription.GetResourceGroups();

var created = await groups.CreateOrUpdateAsync(
    WaitUntil.Completed, "my-rg", new ResourceGroupData(new AzureLocation("eastus")));
Console.WriteLine(created.Value.Data.Name);
await created.Value.DeleteAsync(WaitUntil.Completed);
```

### Managed identity

Use the factory to point the official managed identity credential at the local endpoint:

```csharp
var credential = factory.CreateManagedIdentityCredential();
var arm = factory.CreateArmClient(credential: credential);
var subscription = await arm.GetDefaultSubscriptionAsync();
```

The endpoint validates token requests and returns test tokens with a one-hour expiry. It does not contact Microsoft Entra ID. IMDSv2 key-bound tokens are not supported.

## Use in tests

Start the emulator before your test run and create clients with `AzlocalClientFactory`. Give each test unique resource names to keep test data separate.

For tests that run the host inside the test process, use `WebApplicationFactory<Program>` with a reference to `AzLocal.Host` and `Microsoft.AspNetCore.Mvc.Testing`. Pass `Server.CreateHandler()` to the client factory and configure a separate blob directory for each fixture. The repository's [EmulatorFixture](tests/AzLocal.IntegrationTests/Fixtures/EmulatorFixture.cs) shows the setup.

## State and configuration

The default state store is in memory. Resource metadata, secrets, and messages are lost when the host stops. Blob contents are stored on disk in the system temporary directory under `azlocal/blobs`.

Set `AzLocal:StateMode` to `JsonSnapshot` or `Sqlite` to keep state between runs. You can also configure the state and blob directories. Run one host per state directory.

The blob storage format changed to separate accounts and preserve case-sensitive names. Blob data created with the previous format needs to be recreated; there is no automatic migration. `azlocal reset` clears the default state directory, but leaves custom storage directories alone.

See [Configuration](docs/CONFIGURATION.md) for storage details.

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

```sh
dotnet pack src/AzLocal.Cli -c Release -o artifacts/packages
dotnet tool install --global azlocal --add-source artifacts/packages
```

The tool package includes the host and can run from any directory.

See [Contributing](docs/CONTRIBUTING.md) for the project layout and [Services](docs/SERVICES.md) for service documentation.

## License

[MIT](LICENSE)
