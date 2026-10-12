# Changelog

## Unreleased

- Run the emulator server in Docker with persistent SQLite and certificate volumes.
- Remove the CLI project and global tool from future releases; keep the Client and Core NuGet libraries.
- Test packaged SDK clients and state persistence against the container before publication.
- Publish tested, versioned Linux AMD64 images to Docker Hub through the release workflow.

## 0.1.0-alpha.1

First public testing release, published on NuGet.

- Local Blob Storage and Key Vault Secrets operations through the official .NET SDKs.
- Service Bus queues over AMQP: single and batch sends, peek, receive, settlement, lock renewal, and dead-letter queues.
- Resource groups and subscriptions through the ARM SDK.
- Local managed identity token acquisition.
- Dependency injection through `AddAzLocal`.
- In-memory, JSON snapshot, and SQLite state stores.
- CLI package with a bundled emulator host.

This release targets local development and automated tests. See [SDK compatibility](docs/SDK_COMPAT.md) for the supported operations and remaining gaps. Old blob data has no automatic migration.
