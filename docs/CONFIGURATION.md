# Configuration

Configure the emulator host before starting it. Client registrations made with `AddAzLocal` do not configure the host's state store.

## Host settings template

The Docker image configures SQLite and blob storage under `/data`. Set server options through the `azlocal` service's Compose environment section. The equivalent JSON configuration is:

```json
{
  "AzLocal": {
    "StateMode": "Sqlite",
    "SqlitePath": "/data",
    "SnapshotPath": "/data",
    "BlobStorePath": "/data/blobs",
    "AmqpPort": 5672,
    "AmqpEnabled": true
  }
}
```

Paths refer to directories. The host creates them, then creates `state.db` or `state.json` as appropriate. `SnapshotPath` is used only in `JsonSnapshot` mode; `SqlitePath` is used only in `Sqlite` mode. SQLite needs no separate database server or schema setup.

Configure the server container's environment. Your application's `appsettings.json` configures the application, not the separate server. Environment variables use two underscores in place of the colon:

| Setting | Environment variable | Default |
| --- | --- | --- |
| `AzLocal:StateMode` | `AzLocal__StateMode` | `Sqlite` in Docker |
| `AzLocal:SqlitePath` | `AzLocal__SqlitePath` | `/data` |
| `AzLocal:SnapshotPath` | `AzLocal__SnapshotPath` | `/data` |
| `AzLocal:BlobStorePath` | `AzLocal__BlobStorePath` | `/data/blobs` |
| `AzLocal:AmqpPort` | `AzLocal__AmqpPort` | `5672` |
| `AzLocal:AmqpEnabled` | `AzLocal__AmqpEnabled` | `true` |
| `AzLocal:ContainerNetworking` | `AzLocal__ContainerNetworking` | `false`; Docker image enables it, effective only with `DOTNET_RUNNING_IN_CONTAINER=true` |

Unset or blank storage paths use the defaults. Use `InMemory`, `JsonSnapshot`, or `Sqlite` with this exact spelling. Storage modes retain metadata, secrets, and messages; blob contents are always stored separately on disk.

See the [README storage setup](../README.md#state-and-configuration) for Compose settings. Use a custom directory to keep state outside system temporary storage. Restart the host when changing configuration.

## Local-only security and storage

AzLocal deliberately accepts fake credentials. Docker listeners accept private container traffic; Compose publishes ports only on host loopback. Native listeners remain restricted to loopback. Do not place the emulator behind a public reverse proxy. Run one emulator process per state directory.

Blob storage now separates accounts and encodes account, container, and blob names. Blob names are case-sensitive. This changes the on-disk format and metadata keys; existing development blob data must be recreated. No automatic migration is performed.

Service Bus peek locks expire after 30 seconds. Expired lock tokens cannot settle a message. Queue operations are serialized within one host process.

JSON snapshots are flushed to a temporary file and atomically replaced. This protects against interrupted writes; it does not provide multi-process coordination.

Run the server with Docker Compose; see [Docker setup](DOCKER.md).

## SDK endpoints

HTTP services use `https://127.0.0.1:4566` by default. Service Bus also listens on `127.0.0.1:5672` for AMQP clients. Set `AzLocal:AmqpPort` (or `AzLocal__AmqpPort` as an environment variable) to change the AMQP port. Set `AzLocal:AmqpEnabled` to `false` to disable the AMQP listener, for example in tests that only need HTTP.

AMQP uses the HTTP namespace `local`. Set `options.AmqpPort` in `AddAzLocal` to match the published AMQP port. Compose configures HTTPS and AMQP port mappings independently.

Register clients with `AddAzLocal` and inject `ArmClient` for local subscriptions and resource groups. The registered `ManagedIdentityCredential` routes token requests to the local HTTP endpoint without changing machine-wide settings.
