# Changelog

## 0.1.0-alpha.1

First public testing release, pending publication.

- Local Blob Storage and Key Vault Secrets operations through the official .NET SDKs.
- Service Bus queues over AMQP: single and batch sends, peek, receive, settlement, lock renewal, and dead-letter queues.
- Resource groups and subscriptions through the ARM SDK.
- Local managed identity token acquisition.
- Dependency injection through `AddAzLocal`.
- In-memory, JSON snapshot, and SQLite state stores.
- CLI package with a bundled emulator host.

This release targets local development and automated tests. See [SDK compatibility](docs/SDK_COMPAT.md) for the supported operations and remaining gaps. Old blob data has no automatic migration.
