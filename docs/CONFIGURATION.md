# Configuration

Configure the emulator host before starting it. Client registrations made with `AddAzLocal` do not configure the host's state store.

## Host settings template

When running from source, the host reads `src/AzLocal.Host/appsettings.json`. This `AzLocal` section shows SQLite storage with custom directories; replace the paths with absolute paths for your machine:

```json
{
  "AzLocal": {
    "StateMode": "Sqlite",
    "SqlitePath": "/absolute/path/to/project/.azlocal",
    "SnapshotPath": "/absolute/path/to/project/.azlocal",
    "BlobStorePath": "/absolute/path/to/project/.azlocal/blobs",
    "AmqpPort": 5672,
    "AmqpEnabled": true
  }
}
```

Paths refer to directories. The host creates them, then creates `state.db` or `state.json` as appropriate. `SnapshotPath` is used only in `JsonSnapshot` mode; `SqlitePath` is used only in `Sqlite` mode. SQLite needs no separate database server or schema setup.

For the installed CLI, set environment variables in the shell that runs `azlocal start`. The host process inherits them. Your application's `appsettings.json` does not configure that separate process. Environment variables use two underscores in place of the colon:

| Setting | Environment variable | Default |
| --- | --- | --- |
| `AzLocal:StateMode` | `AzLocal__StateMode` | `InMemory` |
| `AzLocal:SqlitePath` | `AzLocal__SqlitePath` | `<system-temp>/azlocal` |
| `AzLocal:SnapshotPath` | `AzLocal__SnapshotPath` | `<system-temp>/azlocal` |
| `AzLocal:BlobStorePath` | `AzLocal__BlobStorePath` | `<system-temp>/azlocal/blobs` |
| `AzLocal:AmqpPort` | `AzLocal__AmqpPort` | `5672` |
| `AzLocal:AmqpEnabled` | `AzLocal__AmqpEnabled` | `true` |

Unset or blank storage paths use the defaults. Use `InMemory`, `JsonSnapshot`, or `Sqlite` with this exact spelling. Storage modes retain metadata, secrets, and messages; blob contents are always stored separately on disk.

See the [README storage setup](../README.md#sqlite-setup) for PowerShell and shell examples. Use a custom directory to keep state outside system temporary storage. Restart the host when changing configuration.

## Local-only security and storage

AzLocal deliberately accepts fake credentials. Listeners must use localhost or a loopback IP; remote requests are rejected. Do not place it behind a public reverse proxy. Run one emulator process per state directory.

Blob storage now separates accounts and encodes account, container, and blob names. Blob names are case-sensitive. This changes the on-disk format and metadata keys; existing development blob data must be recreated. No automatic migration is performed.

Service Bus peek locks expire after 30 seconds. Expired lock tokens cannot settle a message. Queue operations are serialized within one host process.

JSON snapshots are flushed to a temporary file and atomically replaced. This protects against interrupted writes; it does not provide multi-process coordination.

Build the standalone CLI tool with `dotnet pack src/AzLocal.Cli -c Release -o artifacts/packages`. The package bundles the host and launches it without a source checkout.

## SDK endpoints

HTTP services use `https://127.0.0.1:4566` by default. Service Bus also listens on `127.0.0.1:5672` for AMQP clients. Set `AzLocal:AmqpPort` (or `AzLocal__AmqpPort` as an environment variable) to change the AMQP port. Set `AzLocal:AmqpEnabled` to `false` to disable the AMQP listener, for example in tests that only need HTTP.

AMQP uses the HTTP namespace `local`. Set `options.AmqpPort` in `AddAzLocal` to match a custom AMQP port. The HTTP `--port` option does not change the AMQP port.

Register clients with `AddAzLocal` and inject `ArmClient` for local subscriptions and resource groups. The registered `ManagedIdentityCredential` routes token requests to the local HTTP endpoint without changing machine-wide settings.
