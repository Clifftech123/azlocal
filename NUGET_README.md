![azlocal logo](https://raw.githubusercontent.com/Clifftech123/azlocal/main/assets/branding/icon.png)

# azlocal

A local Azure emulator for .NET development and testing. Use the official Azure SDKs without an Azure subscription.

Requires the .NET 10 SDK. Supports Blob Storage, Key Vault secrets, Service Bus queues, resource groups, and local managed identity tokens. See [supported operations](https://github.com/Clifftech123/azlocal/blob/main/docs/SDK_COMPAT.md) for API coverage.

## Installation

Add the client library to your application project:

```sh
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

The client installs `AzLocal.Core` automatically. Version `0.1.0-alpha.1` is a prerelease.

`AzLocal.Client` configures Azure SDK clients in your application. It does not start or include the emulator server. The `azlocal` package is the separate CLI that bundles the server; application integration does not require globally installing that tool.

## Usage

Register the clients in an ASP.NET Core application's `Program.cs`:

```csharp
using AzLocal.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzLocal(options =>
{
    options.BaseUrl = "https://127.0.0.1:4566";
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
    options.KeyVaultName = "myvault";
    options.ServiceBusQueueName = "myqueue";
});

var app = builder.Build();
app.Run();
```

Inject `BlobContainerClient`, `SecretClient`, `ServiceBusSender`, or `ServiceBusReceiver` into your application services. SDK clients are singletons; your services can be scoped or transient. [Usage examples](https://github.com/Clifftech123/azlocal#connect-your-application) show operations for each service.

## Emulator server

Start the emulator separately before your application or tests make requests. The default endpoints are HTTPS at `https://127.0.0.1:4566` and AMQP at `127.0.0.1:5672`. Match `BaseUrl` and `AmqpPort` to the running server.

The current release supports the CLI and [running the host from source](https://github.com/Clifftech123/azlocal#contributing). See [server setup](https://github.com/Clifftech123/azlocal#install-and-run) and [HTTPS setup](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md). Docker distribution is planned and is not available in this release.

**Windows:** enforced App Control can block the CLI launcher or application DLLs. Adding a project dependency does not resolve that restriction. See the [known signing issue](https://github.com/Clifftech123/azlocal/blob/main/docs/WINDOWS_APP_CONTROL.md).

## Documentation

- [Configuration and storage](https://github.com/Clifftech123/azlocal/blob/main/docs/CONFIGURATION.md)
- [Certificate setup](https://github.com/Clifftech123/azlocal/blob/main/docs/HTTPS.md)
- [Release notes](https://github.com/Clifftech123/azlocal/releases)
- [Report an issue](https://github.com/Clifftech123/azlocal/issues)

MIT licensed. Independent of Microsoft; intended for local development and testing.
