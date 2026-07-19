# azlocal

**Local Azure emulator for .NET — develop and test without a real Azure subscription.**

> **Status: Under active development. Not production-ready.**

---

## The problem

Every time you write .NET code that talks to Azure — Blob Storage, Key Vault, Service Bus — you need a live Azure environment to run and test it. That means a subscription, credentials, internet access, and real cloud costs just to run your tests.

azlocal removes that dependency entirely: it runs a local server that speaks the same HTTP API as Azure, so your code and tests work as if Azure is there.

---

## Get started

### 1. Install the CLI and the client library

```bash
dotnet tool install -g azlocal
dotnet add package AzLocal.Client
```

### 2. Trust the local HTTPS certificate (one-time)

The Azure SDKs require HTTPS, so azlocal runs over HTTPS by default using the standard ASP.NET Core dev certificate:

```bash
azlocal trust-cert
```

### 3. Start the emulator

```bash
azlocal start

# AzLocal started on https://127.0.0.1:4566 (pid 12345)
```

This returns immediately — the host runs as a separate background process. Check it's actually up, or stop it, with:

```bash
azlocal wait     # blocks until ready (useful in scripts/CI)
azlocal status   # prints RUNNING/NOT running
azlocal stop      # stops the host
azlocal reset     # stops the host AND wipes all stored state
```

### 4. Use it

```csharp
using AzLocal.Client;

var factory = new AzlocalClientFactory(); // defaults to https://127.0.0.1:4566

// Instead of connecting to real Azure:
//   new BlobServiceClient(new Uri("https://myaccount.blob.core.windows.net"), new DefaultAzureCredential());
// connect to azlocal — the real Azure SDK client, everything else stays the same:
var blobClient = factory.CreateBlobServiceClient("myaccount");
```

Blob Storage and Key Vault work with the real, unmodified Azure SDK — no mocking, no forks. Service Bus and ARM are also emulated, but only through `AzlocalClientFactory`'s own HTTP client, not the official SDKs (see [Services](#services) for why).

---

## Usage examples

Every example below assumes `var factory = new AzlocalClientFactory();` and that azlocal is running (steps 2–3 above).

### Blob Storage — real `Azure.Storage.Blobs` SDK

```csharp
var container = factory.CreateBlobContainerClient("myaccount", "uploads");
await container.CreateIfNotExistsAsync();

var blob = container.GetBlobClient("hello.txt");
await blob.UploadAsync(new BinaryData("hello from azlocal"), overwrite: true);

var download = await blob.DownloadContentAsync();
Console.WriteLine(download.Value.Content.ToString()); // "hello from azlocal"
```

### Key Vault Secrets — real `Azure.Security.KeyVault.Secrets` SDK

```csharp
var secrets = factory.CreateSecretClient("myvault");

await secrets.SetSecretAsync("my-api-key", "super-secret-value");
var secret = await secrets.GetSecretAsync("my-api-key");
Console.WriteLine(secret.Value.Value); // "super-secret-value"
```

### Service Bus (Queues) — HTTP client, not the official SDK

```csharp
var sb = factory.CreateServiceBusHttpClient("mynamespace");

await sb.PostAsync("myqueue/messages", new StringContent("hello"));       // send
var received = await sb.PostAsync("myqueue/messages/head", null);        // peek-lock receive
var body = await received.Content.ReadAsStringAsync();                   // "hello"

// Complete it using the lock token from the BrokerProperties response header:
var lockToken = /* parse "LockToken" out of received.Headers.GetValues("BrokerProperties") */;
await sb.DeleteAsync($"myqueue/messages/{lockToken}");
```

### ARM (Resource Groups & Subscriptions) — HTTP client, not the official SDK

```csharp
var arm = factory.CreateHttpClient();

// List the (single, fake) configured subscription:
var subs = await arm.GetAsync("/arm/subscriptions");
var subId = /* parse "subscriptionId" out of the JSON "value" array */;

var body = new StringContent("{\"location\":\"eastus\"}", Encoding.UTF8, "application/json");
await arm.PutAsync($"/arm/subscriptions/{subId}/resourcegroups/my-rg", body);
```

---

## Using azlocal in your own tests

**Option A — separately running process.** Start azlocal (steps 2–3 above, e.g. in a CI step or manually), then use `AzlocalFixture` as an xUnit class fixture:

```csharp
public class MyTests : IClassFixture<AzlocalFixture>
{
    private readonly AzlocalFixture _fixture;
    public MyTests(AzlocalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UploadsABlob()
    {
        var container = _fixture.Clients.CreateBlobContainerClient("acct", "container");
        // ...
    }
}
```

**Option B — in-process, no separate process to start/stop.** Reference `AzLocal.Host` directly and boot it with `WebApplicationFactory<Program>` (this is exactly what azlocal's own integration tests do — see `tests/AzLocal.IntegrationTests/Fixtures/EmulatorFixture.cs` for the full pattern):

```csharp
public sealed class EmulatorFixture : WebApplicationFactory<Program>
{
    public AzlocalClientFactory Clients { get; }
    public EmulatorFixture() => Clients = new AzlocalClientFactory("https://127.0.0.1", Server.CreateHandler());
}
```

This needs a package reference to `AzLocal.Host` and the `Microsoft.AspNetCore.Mvc.Testing` package. It's faster (no process startup) and fully isolated per test run.

---

## Who this is for

- **.NET developers** who want to run and test Azure-dependent code on their laptop without a live Azure environment
- **CI/CD pipelines** that need Azure services available during automated tests without cloud costs or credential management

---

## Services

| Phase | Service | Status | Azure SDK compatible? |
|---|---|---|---|
| 1 | Blob Storage | Implemented | Yes — `Azure.Storage.Blobs`, real SDK, no code changes |
| 1 | Key Vault Secrets | Implemented | Yes — `Azure.Security.KeyVault.Secrets`, real SDK, no code changes |
| 1 | Resource Groups & Subscriptions (ARM) | Implemented | No — HTTP only, via `AzlocalClientFactory.CreateHttpClient()`, not `Azure.ResourceManager` |
| 1 | Managed Identity (IMDS stub) | Implemented | Yes — any `TokenCredential`-based SDK call acquires a token transparently |
| 2 | Service Bus (Queues) | Implemented | No — HTTP only, via `AzlocalClientFactory.CreateServiceBusHttpClient()`, not `Azure.Messaging.ServiceBus` (that SDK speaks AMQP over TCP, which azlocal doesn't implement) |
| 2 | Queue Storage | Planned | — |
| 2 | Table Storage | Planned | — |
| 2 | App Configuration | Planned | — |
| 3 | Cosmos DB (SQL API) | Planned | — |
| 3 | Event Grid | Planned | — |
| 3 | Azure Functions (HTTP trigger) | Planned | — |

See [docs/SDK_COMPAT.md](docs/SDK_COMPAT.md) for the details behind each "No", and what it would take to close the gap.

---

## CLI reference

| Command | What it does |
|---|---|
| `start [--port N]` | Starts the host as a background process (default port 4566) |
| `stop` | Stops the running host |
| `reset` | Stops the host and deletes all stored state (`%TEMP%/azlocal`) |
| `wait [--port N] [--timeout S]` | Blocks until the host responds, or times out |
| `status [--port N]` | Prints whether a host is running on the given port |
| `trust-cert` | Trusts the local HTTPS dev certificate (`dotnet dev-certs https --trust`) |

---

## Using in CI (GitHub Actions)

```yaml
- name: Install azlocal
  run: dotnet tool install -g azlocal

- name: Start azlocal
  run: azlocal start

- name: Wait for azlocal to be ready
  run: azlocal wait

- name: Run tests
  run: dotnet test

- name: Stop azlocal
  run: azlocal stop
```

Only needed if your tests connect to a separately-running azlocal process. If you use Option B from [Using azlocal in your own tests](#using-azlocal-in-your-own-tests) instead, `dotnet test` alone is enough — no `start`/`stop` steps required.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

---

## Contributing / working on azlocal itself

Building azlocal from source (rather than installing it as a package) is only needed if you're working on azlocal itself:

```bash
git clone <repo-url>
cd azlocal
dotnet build
dotnet test
```

`dotnet test` runs both the unit test suite (handler/store/middleware/CLI logic in isolation) and the integration suite, which exercises the real Azure SDKs against an in-process host via `WebApplicationFactory` — no separately-started process needed.

```
src/
  AzLocal.Host/          # Web host — receives all requests on :4566 (HTTPS by default)
  AzLocal.Core/          # Shared interfaces, models, and request routing
  AzLocal.Middleware/    # Auth stub, request logging, IMDS stub, Key Vault auth-challenge responder
  AzLocal.Services/      # Service handlers (one per Azure service)
  AzLocal.State/         # State backends — in-memory, SQLite, JSON snapshot
  AzLocal.Cli/           # CLI tool (azlocal start / stop / reset / wait / trust-cert)
  AzLocal.Client/        # Client library — pre-configured SDK client factory

tests/
  AzLocal.UnitTests/         # Handler/store/middleware/CLI logic in isolation, no HTTP
  AzLocal.IntegrationTests/  # Real Azure SDKs against an in-process host (WebApplicationFactory)
  AzLocal.CompatTests/
```

---

## License

MIT
