# Azure SDK compatibility

azlocal supports the operations listed below with the official .NET SDKs. It does not implement the full Azure service APIs.

| SDK | Tested local operations |
| --- | --- |
| `Azure.Storage.Blobs` | Container lifecycle; upload, download, list, delete, properties, and existence checks |
| `Azure.Security.KeyVault.Secrets` | Set, get, list, delete, and read secret versions |
| `Azure.Messaging.ServiceBus` | Send single messages and batches; peek; receive with peek-lock or receive-and-delete; complete, abandon, renew locks, dead-letter, and read dead-letter queues |
| `Azure.ResourceManager` | Discover the configured subscription; create or update, get, list, check existence, and delete resource groups; preserve tags |
| `Azure.Identity` | Acquire a local managed identity token and use it with an ARM client |

## Client setup

Register SDK clients with `services.AddAzLocal(options => { ... })` and inject them into your application services. The default HTTP endpoint is `https://127.0.0.1:4566`.

Set `BlobAccountName`, `BlobContainerName`, `KeyVaultName`, and `ServiceBusQueueName` for the clients you need. `BaseUrl` and `AmqpPort` select the HTTP and AMQP endpoints. See the [README example](../README.md#dependency-injection).

## Blob Storage and Key Vault

Trust the development certificate before using the HTTP SDK clients. Token-based Blob Storage clients need HTTPS. Use the literal IP `127.0.0.1` so the Blob SDK recognizes account paths.

See [HTTPS setup](HTTPS.md) for certificate trust, Linux diagnostics, older certificates, and CI setup.

Key Vault uses a local authentication challenge. The registered client disables challenge resource verification for the local endpoint. This setting is intended for the emulator.

## Service Bus

The host starts an AMQP 1.0 TCP listener on `127.0.0.1:5672`. The registered client uses `UseDevelopmentEmulator=true` and fake SAS credentials. The listener is bound to loopback, and connections are plaintext.

AMQP clients use the HTTP namespace `local`. Queues are created on first use. HTTP and AMQP operations use the same state store and coordinator, so they share messages and locks.

Locks expire after 30 seconds. Expired tokens cannot settle a message. The local message or batch limit is 256 KiB. Use one host per state directory.

Topics, sessions, transactions, scheduled delivery, message deferral, administration APIs, and AMQP over WebSockets are not implemented. Unsupported management operations return an error rather than reporting success.

## Resource management

The host serves the standard `/subscriptions/.../resourceGroups/...` routes used by the SDK. The existing `/arm/subscriptions/...` HTTP routes remain available.

Resource-group operations finish immediately. SDK calls with `WaitUntil.Completed` are tested for creation and deletion. Other Azure resource types and asynchronous deployment operations are not implemented.

## Managed identity

The registered `ManagedIdentityCredential` routes IMDS requests to the local host. It does not change global environment variables or send requests to Azure's metadata address.

The token endpoint requires GET, `Metadata: true`, an API version, and an absolute resource URI. It returns the configured fake token, the requested resource, and a one-hour expiry. Discovery responses allow the official credential to select the local IMDSv1 flow.

Tokens are for local tests and are not issued or validated by Microsoft Entra ID. IMDSv2 key-bound tokens are not implemented. The default `AzlocalCredential` remains available for clients that only need a static test credential.

## Tests

[AzLocal.CompatTests](../tests/AzLocal.CompatTests) calls the official ARM, Service Bus, and managed identity SDKs. Blob Storage and Key Vault SDK tests are in [AzLocal.IntegrationTests](../tests/AzLocal.IntegrationTests).

## Dependency injection

Call `services.AddAzLocal(options => { ... })` to register SDK clients as singletons. Set `BlobAccountName`, `BlobContainerName`, `KeyVaultName`, and `ServiceBusQueueName` for the clients your application injects. `BaseUrl` and `AmqpPort` select the HTTP and AMQP endpoints. See the [README example](../README.md#dependency-injection).

The container owns the Service Bus client and registered senders and receivers. Use asynchronous disposal for a manually built service provider. Existing client registrations are preserved. Application services can use scoped or transient lifetimes while sharing the singleton SDK clients.
