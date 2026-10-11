# HTTPS and certificate trust

azlocal's HTTP services use HTTPS. The default endpoint is `https://127.0.0.1:4566`. Blob Storage token authentication also requires HTTPS.

## First run

Install the .NET 10 SDK, then run certificate setup as the same user who will start azlocal:

```sh
azlocal trust-cert
dotnet dev-certs https --check --trust
azlocal start
azlocal wait
```

`azlocal trust-cert` runs `dotnet dev-certs https --trust`. This creates a development certificate if needed and asks the SDK to trust it. Accept the operating system's confirmation prompt if one appears. Running `azlocal start` does not trust certificates automatically.

If you are running from source or the tool is not installed yet, use the .NET command directly:

```sh
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
```

The check command verifies certificate presence and trust without creating a new certificate. The .NET 10 certificate includes `127.0.0.1`, which is the address used by the Blob SDK setup. See the [official certificate command documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs).

## Windows and macOS

The SDK uses your user certificate store or keychain. Approve the trust prompt when requested. Restart azlocal and your application after changing certificate settings. Run setup from the same account as the host; a certificate created under another account may not be available to it.

## Linux and WSL

Use the .NET SDK's trust flow rather than running the emulator as root:

```sh
dotnet dev-certs https --trust --verbose
dotnet dev-certs https --check --trust
```

Linux trust depends on the distribution and the HTTP client. Follow the SDK's diagnostic output for any missing trust-store tools or `SSL_CERT_DIR` settings. If it prints an `SSL_CERT_DIR` value, export that value in the shell that launches the application and azlocal. Browser trust and .NET HTTP client trust may use different stores.

In .NET 10, trusting a certificate inside WSL can also update trust on the Windows host. See [Microsoft's platform-specific trust instructions](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0#trust-the-aspnet-core-https-development-certificate).

## Check the connection

After setup:

```sh
azlocal status
curl https://127.0.0.1:4566/
```

Use `curl.exe` for this command in Windows PowerShell 5, where `curl` can be an alias. A successful response contains `AzLocal emulator running`. Curl may use a different certificate store from .NET; `dotnet dev-certs https --check --trust` and an SDK request check the application setup more directly.

`azlocal wait` and `azlocal status` make HTTPS requests with certificate validation. A timeout or `NOT running` can mean an untrusted certificate, even if the host process started. They do not currently distinguish that from a connection failure.

## Older development certificates

A certificate created by an older SDK may lack the `127.0.0.1` subject alternative name. If trust checks pass but requests fail with a hostname mismatch, inspect the certificate and follow the SDK's instructions to recreate it with .NET 10. Cleaning development certificates also affects other local ASP.NET Core projects, so do this only when needed. Restart the host after replacing the certificate.

## CI and in-process tests

For an external host, configure a certificate and client trust in the CI job that runs it. The repository's [package installation check](../eng/Test-Packages.ps1) creates a temporary certificate, configures Kestrel with it, and pins that certificate in its consumer app. It does not modify the runner's trust stores or disable certificate checks for SDK calls.

For `WebApplicationFactory` tests, pass the test server's handler to `AddAzLocal` through `TransportHandler`. HTTP requests stay in process and do not need machine-wide certificate trust. Service Bus still uses its separate loopback AMQP listener.
