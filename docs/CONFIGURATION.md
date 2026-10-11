
## Local-only security and storage

AzLocal deliberately accepts fake credentials. Listeners must use localhost or a loopback IP; remote requests are rejected. Do not place it behind a public reverse proxy. Run one emulator process per state directory.

Blob storage now separates accounts and encodes account, container, and blob names. Blob names are case-sensitive. This changes the on-disk format and metadata keys; existing development blob data must be recreated. No automatic migration is performed.

Service Bus peek locks expire after 30 seconds. Expired lock tokens cannot settle a message. Queue operations are serialized within one host process.

JSON snapshots are flushed to a temporary file and atomically replaced. This protects against interrupted writes; it does not provide multi-process coordination.

Build the standalone CLI tool with `dotnet pack src/AzLocal.Cli -c Release -o artifacts/packages`. The package bundles the host and launches it without a source checkout.

## SDK endpoints

HTTP services use `https://127.0.0.1:4566` by default. Service Bus also listens on `127.0.0.1:5672` for AMQP clients. Set `AzLocal:AmqpPort` (or `AzLocal__AmqpPort` as an environment variable) to change the AMQP port. Set `AzLocal:AmqpEnabled` to `false` to disable the AMQP listener, for example in tests that only need HTTP.

AMQP uses the HTTP namespace `local`. Pass a custom AMQP port to `CreateServiceBusClient(amqpPort: ...)`. The HTTP `--port` option does not change the AMQP port.

Use `CreateArmClient()` for local subscriptions and resource groups. Use `CreateManagedIdentityCredential()` to route official managed identity token requests to the local HTTP endpoint without changing machine-wide settings.
