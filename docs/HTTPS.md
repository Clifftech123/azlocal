# HTTPS setup

The Docker server generates its development HTTPS certificate on first startup and retains it in a named volume. It is separate from your operating system's ASP.NET development certificate.

## Export the public certificate

```sh
docker compose cp azlocal:/certs/localhost.crt ./azlocal-localhost.crt
```

Export only this public certificate. The PFX contains the private key and should remain in the container volume.

Trust the certificate in the application's certificate store, or pin its thumbprint in a local-development HTTP transport. The [Docker guide](DOCKER.md#https-and-application-clients) and [README DI example](../README.md#dependency-injection) show pinning without changing machine-wide trust.

The certificate includes `localhost`, `127.0.0.1`, and `::1`. Use `127.0.0.1` in the client factory so the Blob SDK recognizes account paths. The current certificate lasts one year. Replacing the certificate volume requires updating application trust or pins.

## Check the server

```sh
docker compose ps
docker compose logs azlocal
```

The container's health check pins its own certificate. A healthy container does not imply that a separate application's certificate store trusts it. If SDK calls fail with a certificate error, check the exported certificate and application pin/trust.

For applications/tests in Docker, share the emulator's network namespace and supply its public certificate. The repository's consumer container demonstrates this setup:

```sh
docker compose --profile checks build checks
docker compose --profile checks run --rm checks
```

Do not disable certificate validation globally. An explicit certificate pin in local tests validates the selected server identity.

## Windows application policies

HTTPS trust and Windows executable trust are separate. An HTTPS certificate does not resolve App Control blocking an application assembly. See [Windows App Control](WINDOWS_APP_CONTROL.md) and the [containerized application guidance](DOCKER.md#applications-and-tests-inside-docker).

The global CLI and its certificate-trust command were removed when server distribution moved to Docker. Instructions embedded in the old alpha package refer to that historical release.
