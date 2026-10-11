![azlocal logo](https://raw.githubusercontent.com/Clifftech123/azlocal/main/assets/branding/icon.png)

# azlocal

A local Azure emulator for .NET development and testing. Use the official Azure SDKs without an Azure subscription.

Requires the .NET 10 SDK. Supports Blob Storage, Key Vault secrets, Service Bus queues, resource groups, and local managed identity tokens. See [supported operations](https://github.com/Clifftech123/azlocal/blob/main/docs/SDK_COMPAT.md) for API coverage.

## Installation

Install and start the emulator:

```sh
dotnet tool install --global azlocal --version 0.1.0-alpha.1
azlocal trust-cert
azlocal start
azlocal wait
```

Run certificate setup as the same user who starts the host and approve the trust prompt. For Linux, WSL, or CI, see [HTTPS setup](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md).

Add the client library to your application project:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

The client installs `AzLocal.Core` automatically. Version `0.1.0-alpha.1` is a prerelease.

## Usage

Register the clients in an ASP.NET Core application's `Program.cs`:

```csharp
using AzLocal.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
    options.KeyVaultName = "myvault";
    options.ServiceBusQueueName = "myqueue";
});

var app = builder.Build();
app.Run();
```

Inject `BlobContainerClient`, `SecretClient`, `ServiceBusSender`, or `ServiceBusReceiver` into your application services. SDK clients are singletons; your services can be scoped or transient. [Usage examples](https://github.com/Clifftech123/azlocal#connect-your-application) show operations for each service.

The host uses HTTPS at `https://127.0.0.1:4566` and AMQP at `127.0.0.1:5672`. Use `azlocal status` to check it and `azlocal stop` to stop it. Registration does not start the emulator.

## Documentation

- [Configuration and storage](https://github.com/Clifftech123/azlocal/blob/main/docs/CONFIGURATION.md)
- [Certificate setup](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md)
- [Release notes](https://github.com/Clifftech123/azlocal/releases)
- [Report an issue](https://github.com/Clifftech123/azlocal/issues)

MIT licensed. Independent of Microsoft; intended for local development and testing.
