# Contributing to azlocal

Building from source (rather than installing it as a package) is only needed if you're working on azlocal itself.

```bash
git clone https://github.com/Clifftech123/azlocal.git
cd azlocal
dotnet build
dotnet test
```

`dotnet test` runs both the unit test suite (handler/store/middleware logic in isolation) and the integration suite, which exercises the real Azure SDKs against an in-process host via `WebApplicationFactory` — no separately-started process needed.

## Project structure

```
src/
  AzLocal.Host/          # Web host — receives all requests on :4566 (HTTPS by default)
  AzLocal.Core/          # Shared interfaces, models, and request routing
  AzLocal.Middleware/    # Auth stub, request logging, IMDS stub, Key Vault auth-challenge responder
  AzLocal.Services/      # Service handlers (one per Azure service)
  AzLocal.State/         # State backends — in-memory, SQLite, JSON snapshot
  AzLocal.Client/        # Client library — DI registrations and configured Azure SDK clients

tests/
  AzLocal.UnitTests/         # Handler/store/middleware logic in isolation, no HTTP
  AzLocal.IntegrationTests/  # Real Azure SDKs against an in-process host (WebApplicationFactory)
  AzLocal.CompatTests/
```

## Package versions

NuGet versions are managed in [Directory.Packages.props](../Directory.Packages.props). Projects list the packages they use with versionless `PackageReference` entries. To add a dependency, add its `PackageVersion` centrally and its `PackageReference` to the project. To update a dependency, change its central version and run `dotnet restore` and `dotnet test`.

Release versions are separate from dependency versions. MinVer calculates assembly and package versions from Git tags for every project. Its shared settings are in `Directory.Build.props`; its dependency version is in `Directory.Packages.props`. See [Releasing](RELEASING.md) for packing and testing the NuGet artifacts.

## Docker checks

Run `docker compose up --build --detach --wait azlocal`, then `docker compose --profile checks build checks` and `docker compose --profile checks run --rm checks`. The [Docker guide](DOCKER.md) explains certificates, volumes, and isolated verification with `eng/Test-Docker.ps1`.
