# Run azlocal with Docker

The emulator runs in a Linux container. Your application can run on your machine or in another container. Docker Desktop must use Linux containers on Windows or macOS; Linux can use Docker Engine with the Compose plugin.

## Build and run from the repository

No global .NET tool or host-side SDK installation is required for the server:

```sh
git clone https://github.com/Clifftech123/azlocal.git
cd azlocal
docker compose up --build --detach --wait azlocal
```

For changes awaiting merge, check out `develop` before building. The image uses the official .NET SDK at build time and an ASP.NET runtime at execution. It runs as a non-root user with a read-only filesystem, except for its volumes and temporary files.

Compose publishes HTTPS on `127.0.0.1:4566` and Service Bus AMQP on `127.0.0.1:5672`. Keep these port mappings bound to host loopback. The server emulates authentication and must not be exposed publicly.

```sh
docker compose ps
docker compose logs azlocal
docker compose stop azlocal
docker compose start azlocal
docker compose down
```

## Use Docker Hub images

The release workflow publishes versioned images to `clifftech123/azlocal` after the Docker and cross-platform checks pass. The first image requires a release containing Docker support; the earlier NuGet-only `0.1.0-alpha.1` release has no image. Verify available tags on [Docker Hub](https://hub.docker.com/r/clifftech123/azlocal/tags).

Use `compose.hub.yaml` and a published tag. PowerShell:

```powershell
$env:AZLOCAL_VERSION = "<published-image-tag>"
docker compose -f compose.hub.yaml pull
docker compose -f compose.hub.yaml up --detach --wait
```

Linux or macOS shell:

```sh
export AZLOCAL_VERSION="<published-image-tag>"
docker compose -f compose.hub.yaml pull
docker compose -f compose.hub.yaml up --detach --wait
```

Use versioned tags for reproducible environments. Prerelease images never update `latest`; stable releases do. The first published images support `linux/amd64`. ARM64 publishing and testing are not included yet.

## HTTPS and application clients

On first startup, the container creates an HTTPS certificate in the `azlocal-certs` volume. It covers `localhost`, `127.0.0.1`, and `::1`, lasts one year, and is reused across restarts. Export only its public certificate:

```sh
docker compose cp azlocal:/certs/localhost.crt ./azlocal-localhost.crt
```

Trust the exported certificate in the application's certificate store, or pin that specific certificate for local development and tests. Do not export the PFX/private key or disable certificate validation globally.

For an application already using a `WebApplicationBuilder` named `builder`, pin the public certificate:

```csharp
using AzLocal.Client;
using System.Security.Cryptography.X509Certificates;

using var certificate = X509CertificateLoader.LoadCertificateFromFile("azlocal-localhost.crt");
var thumbprint = certificate.Thumbprint;
using var transport = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, peer, _, _) =>
        peer?.Thumbprint == thumbprint
};
builder.Services.AddAzLocal(options =>
{
    options.BaseUrl = "https://127.0.0.1:4566";
    options.BlobAccountName = "myaccount";
    options.BlobContainerName = "uploads";
    options.TransportHandler = transport;
});
```

Keep the transport alive until the application and its clients are disposed. Use this only for local development or tests. AMQP uses a separate TCP connection; the HTTP handler does not affect it. See [HTTPS setup](HTTPS.md).

## Applications and tests inside Docker

The repository includes a consumer container that exercises Blob Storage, Key Vault, Service Bus, ARM, managed identity, and DI. It shares the server's network namespace, allowing the client's loopback endpoints to work, and pins the certificate. Run:

```sh
docker compose --profile checks build checks
docker compose --profile checks run --rm checks
```

For your own application/tests, use the same `network_mode: service:azlocal` pattern. Mount only the public certificate in a real application, or provide a trusted certificate for the setup. Both services must belong to the same Compose project; application ports must be published on the service whose network namespace is shared.

`AddAzLocal` currently requires loopback addresses. Docker DNS names such as `https://azlocal:4566` are not supported; sharing the namespace keeps `127.0.0.1` valid. Docker does not change signing policies for applications still running directly on Windows.

## Storage and reset

SQLite is the container default: `/data/state.db`, with blob files at `/data/blobs`. The `azlocal-data` volume retains them across container recreation. The separate certificate volume keeps HTTPS identity stable. In-memory and JSON modes remain available via `AzLocal__StateMode`.

For `JsonSnapshot`, also set `AzLocal__SnapshotPath=/data`. `InMemory` discards metadata and messages when the process stops; blob files remain unless the volume is removed.

`docker compose down` preserves volumes. **`docker compose down --volumes` deletes this Compose project's data and certificates.** Recreating a certificate requires updating client trust or pinning. Do not use that command when you want to keep data.

Set `AZLOCAL_HTTPS_PORT` and `AZLOCAL_AMQP_PORT` to change host ports; match the host-side client's `BaseUrl` and `AmqpPort`. Shared-network consumer containers still use internal ports `4566` and `5672`.

## Verify a change

With PowerShell 7 and Docker:

```powershell
./eng/Test-Docker.ps1
```

The check builds the image and consumer, uses unique temporary containers/volumes, tests the published HTTPS port, exercises SDK operations, restarts the server, and verifies metadata and blobs persist. It removes only its own test resources. CI runs it before Docker Hub publication.
