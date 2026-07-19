# Azure SDK compatibility

azlocal's goal is that real Azure SDK code works against it unchanged. That's true today for
Blob Storage and Key Vault Secrets. It is **not** true for Service Bus or ARM — this page
explains why, and what actually works instead.

## ✅ Blob Storage — `Azure.Storage.Blobs`

Fully compatible. `AzlocalClientFactory.CreateBlobServiceClient`/`CreateBlobContainerClient`/
`CreateBlobClient` return real `Azure.Storage.Blobs` SDK clients, and every method that
matters works against them: upload, download, list, delete, properties, `ExistsAsync`,
`CreateIfNotExistsAsync`, etc.

Two things had to line up for this to work, both already handled by
`AzlocalClientFactory`/`EmulatorDefaults` — you don't need to do anything extra, but they
explain constraints you'll hit if you construct clients yourself instead of using the factory:

- **The emulator must be HTTPS.** The Storage SDK refuses to send a `TokenCredential`-based
  bearer token over plain HTTP. Run `azlocal trust-cert` once so the local dev certificate is
  trusted.
- **Blob routes live at the URL root, and the host must be a literal IP.** The SDK's
  `BlobUriBuilder` only recognizes "path-style" addressing (`{host}/{account}/{container}/{blob}`,
  the same scheme Azurite uses) when the host is an IP address — `127.0.0.1`, not `localhost`.
  Any extra path prefix (azlocal used to have `/azu/...`) breaks the SDK's own parsing of
  account/container names out of URLs it builds internally (e.g.
  `containerClient.GetBlobClient(name)`), even though a hand-rolled HTTP request to the same
  URL works fine. That's why `EmulatorDefaults.BaseUrl` is `https://127.0.0.1:4566`.

## ✅ Key Vault Secrets — `Azure.Security.KeyVault.Secrets`

Fully compatible. `AzlocalClientFactory.CreateSecretClient` returns a real `SecretClient`.
Set/get/delete/list secrets and version history all work.

This one needed more emulator-side work than Blob, because the Key Vault SDK's auth flow is
stricter:

- **Challenge-based auth.** Unlike Storage, the Key Vault SDK never attaches a bearer token
  optimistically — it first sends an anonymous, bodyless probe request expecting a `401` with a
  `WWW-Authenticate` challenge header, then retries with a token and the real request body.
  `KeyVaultChallengeMiddleware` answers that challenge for `/kv/*` requests missing an
  `Authorization` header; without it, the SDK's bodyless probe (not your real request) is what
  reaches the handler.
- **Challenge resource verification.** The SDK also checks that the challenge's `resource`
  domain matches the request host, as an anti-phishing measure — which a local emulator can
  never satisfy (it's not `*.vault.azure.net`). `AzlocalClientFactory.CreateSecretClient` sets
  `SecretClientOptions.DisableChallengeResourceVerification = true` to allow this.
- **Response `id` shape.** The SDK's `KeyVaultIdentifier.Parse` expects secret `id` URLs shaped
  exactly `{host}/secrets/{name}/{version}` — real Azure encodes the vault name in a subdomain,
  not a path segment. azlocal's routes are still `/kv/{vault}/secrets/...` (needed so Key Vault,
  Blob, Service Bus, and ARM can share one port), but the `id` returned in JSON responses omits
  the `/kv/{vault}` prefix so the SDK can parse it.

## ⚠️ Service Bus (Queues) — HTTP only, not `Azure.Messaging.ServiceBus`

**Not compatible with the real SDK, and can't be** without a much larger investment: the real
`Azure.Messaging.ServiceBus` SDK speaks **AMQP 1.0 over TCP**, not HTTP. azlocal's Service Bus
emulation is a custom HTTP REST facade (`/sb/{namespace}/{queue}/messages`, peek-lock semantics,
dead-lettering) — a different wire protocol entirely. The real SDK will never even attempt to
connect to it.

Use `AzlocalClientFactory.CreateServiceBusHttpClient(namespace)` instead — it returns a
pre-authenticated `HttpClient` for calling the emulator's HTTP endpoints directly:

```csharp
var sb = factory.CreateServiceBusHttpClient("mynamespace");
await sb.PostAsync("myqueue/messages", new StringContent("hello"));                 // send
var received = await sb.PostAsync("myqueue/messages/head", content: null);          // peek-lock receive
```

Making the real SDK work would mean implementing an AMQP 1.0 listener (links, sessions, flow
control) — a substantially different undertaking from the REST work the rest of azlocal does.

## ⚠️ ARM (Resource Groups & Subscriptions) — HTTP only, not `Azure.ResourceManager`

ARM's real REST API is plain HTTPS (unlike Service Bus), so `Azure.ResourceManager` could in
principle work against a compatible emulator — but azlocal doesn't attempt that SDK's full
surface today (long-running-operation polling, resource identifier composition, etc.). Use
`AzlocalClientFactory.CreateHttpClient()` and call the ARM REST routes directly
(`/arm/subscriptions`, `/arm/subscriptions/{id}/resourcegroups/{name}`) instead.
