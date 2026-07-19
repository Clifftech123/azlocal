# Contributing to azlocal

Building from source (rather than installing it as a package) is only needed if you're working on azlocal itself.

```bash
git clone <repo-url>
cd azlocal
dotnet build
dotnet test
```

`dotnet test` runs both the unit test suite (handler/store/middleware/CLI logic in isolation) and the integration suite, which exercises the real Azure SDKs against an in-process host via `WebApplicationFactory` — no separately-started process needed.

## Project structure

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
