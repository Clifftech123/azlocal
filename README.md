# azlocal

**Local Azure emulator for .NET — develop and test without a real Azure subscription.**

> **Status: Under active development. Not production-ready.**

---

## The problem

Every time you write .NET code that talks to Azure — Blob Storage, Key Vault, Service Bus — you need a live Azure environment to run and test it. That means a subscription, credentials, internet access, and real cloud costs just to run your tests.

azlocal removes that dependency entirely.

---

## What azlocal does

azlocal runs a local server on your machine that speaks the same HTTP API as Azure. Your Azure SDK clients connect to it exactly as they would connect to real Azure — no code changes, no special mocking, no SDK forks.

You start azlocal once, then your code and tests work as if Azure is there.

```bash
# Start the emulator
azlocal start

# Emulator is now running at http://localhost:4566
# All Azure services respond on that single port
```

In your C# code or test project:

```csharp
// Instead of connecting to real Azure:
var blobClient = new BlobServiceClient(
    new Uri("https://myaccount.blob.core.windows.net"),
    new DefaultAzureCredential()
);

// Connect to azlocal — everything else stays the same:
var factory = new AzlocalClientFactory();
var blobClient = factory.CreateBlobServiceClient("myaccount");
```

Upload a blob, read a Key Vault secret, send a Service Bus message — it all works locally with no network, no subscription, and no credentials needed.

---

## Who this is for

- **.NET developers** who want to run and test Azure-dependent code on their laptop without a live Azure environment
- **CI/CD pipelines** that need Azure services available during automated tests without cloud costs or credential management

---

## Services

| Phase | Service | Status |
|---|---|---|
| 1 | Blob Storage | Planned |
| 1 | Key Vault Secrets | Planned |
| 1 | Resource Groups & Subscriptions | Planned |
| 1 | Managed Identity (IMDS stub) | Planned |
| 2 | Queue Storage | Planned |
| 2 | Table Storage | Planned |
| 2 | Service Bus (Queues) | Planned |
| 2 | App Configuration | Planned |
| 3 | Cosmos DB (SQL API) | Planned |
| 3 | Event Grid | Planned |
| 3 | Azure Functions (HTTP trigger) | Planned |

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

---

## Build from source

```bash
git clone <repo-url>
cd azlocal
dotnet build
dotnet test
```

---

## Using in CI (GitHub Actions)

```yaml
- name: Start azlocal
  run: |
    dotnet tool install -g azlocal
    azlocal start --background
    azlocal wait

- name: Run tests
  run: dotnet test

- name: Stop azlocal
  run: azlocal stop
```

---

## Project structure

```
src/
  AzLocal.Host/          # Web host — receives all requests on :4566
  AzLocal.Core/          # Shared interfaces and models
  AzLocal.Middleware/    # Auth stub, request logging, IMDS stub
  AzLocal.Services/      # Service handlers (one per Azure service)
  AzLocal.State/         # State backends — in-memory, SQLite, JSON snapshot
  AzLocal.Cli/           # CLI tool (azlocal start / stop / reset / wait)
  AzLocal.Client/        # NuGet package — pre-configured SDK client factory

tests/
  AzLocal.UnitTests/
  AzLocal.IntegrationTests/
  AzLocal.CompatTests/
```



## License

MIT
