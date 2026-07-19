using AzLocal.Core.Interfaces;
using AzLocal.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzLocal.Services.KeyVault;

public class KeyVaultSecretHandler : IServiceHandler
{
    private readonly IStateStore _state;
    private readonly ILogger<KeyVaultSecretHandler> _logger;
    private readonly string _baseUrl;

    public string ServiceName => "KeyVault";

    public KeyVaultSecretHandler(IStateStore state, ILogger<KeyVaultSecretHandler> logger, IConfiguration config)
    {
        _state = state;
        _logger = logger;
        _baseUrl = (config["AzLocal:BaseUrl"] ?? "https://127.0.0.1").TrimEnd('/');
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(KeyVaultRoutes.Secrets, ListSecretsAsync);
        app.MapPut(KeyVaultRoutes.SecretByName, SetSecretAsync);
        app.MapGet(KeyVaultRoutes.SecretByName, GetSecretAsync);
        app.MapGet(KeyVaultRoutes.SecretVersion, GetSecretVersionAsync);
        app.MapDelete(KeyVaultRoutes.SecretByName, DeleteSecretAsync);
    }

    #region Route handlers

    private async Task<IResult> ListSecretsAsync(string vault, HttpContext ctx)
    {
        var secrets = await _state.ListAsync<KeyVaultSecret>($"keyvault/secrets/{vault}/");
        _logger.LogDebug("ListSecrets vault={Vault} count={Count}", vault, secrets.Count);

        // Return only the latest enabled version of each secret — values are never included in list responses.
        var latest = secrets
            .Where(s => s.Enabled)
            .GroupBy(s => s.Name)
            .Select(g => g.OrderByDescending(s => s.UpdatedOn).First())
            .Select(s => new { id = SecretUrl(s.Name, s.Version), attributes = Attributes(s) });

        SetRequestId(ctx);
        return Results.Ok(new { value = latest });
    }

    private async Task<IResult> SetSecretAsync(string vault, string secretName, HttpContext ctx)
    {
        string value;
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            value = doc.RootElement.GetProperty("value").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            _logger.LogWarning(ex, "SetSecret bad request vault={Vault} secret={Secret}", vault, secretName);
            return Results.BadRequest("Request body must be JSON with a 'value' property.");
        }

        var secret = new KeyVaultSecret
        {
            Name = secretName,
            Value = value,
            Version = Guid.NewGuid().ToString("N"),
            CreatedOn = DateTimeOffset.UtcNow,
            UpdatedOn = DateTimeOffset.UtcNow
        };
        secret.Id = SecretUrl(secretName, secret.Version);

        await _state.SetAsync(SecretKey(vault, secretName, secret.Version), secret);
        _logger.LogInformation("Secret set vault={Vault} secret={Secret} version={Version}", vault, secretName, secret.Version);

        SetRequestId(ctx);
        return Results.Ok(SecretResponse(secret));
    }

    private async Task<IResult> GetSecretAsync(string vault, string secretName, HttpContext ctx)
    {
        var all = await _state.ListAsync<KeyVaultSecret>($"keyvault/secrets/{vault}/{secretName}/");
        var secret = all.Where(s => s.Enabled).OrderByDescending(s => s.UpdatedOn).FirstOrDefault();
        if (secret is null)
        {
            _logger.LogWarning("Secret not found vault={Vault} secret={Secret}", vault, secretName);
            return Results.NotFound();
        }

        SetRequestId(ctx);
        return Results.Ok(SecretResponse(secret));
    }

    private async Task<IResult> GetSecretVersionAsync(string vault, string secretName, string version, HttpContext ctx)
    {
        var secret = await _state.GetAsync<KeyVaultSecret>(SecretKey(vault, secretName, version));
        if (secret is null)
        {
            _logger.LogWarning("Secret version not found vault={Vault} secret={Secret} version={Version}", vault, secretName, version);
            return Results.NotFound();
        }

        SetRequestId(ctx);
        return Results.Ok(SecretResponse(secret));
    }

    private async Task<IResult> DeleteSecretAsync(string vault, string secretName, HttpContext ctx)
    {
        var all = await _state.ListAsync<KeyVaultSecret>($"keyvault/secrets/{vault}/{secretName}/");
        foreach (var s in all)
        {
            s.Enabled = false;
            await _state.SetAsync(SecretKey(vault, secretName, s.Version), s);
        }

        _logger.LogInformation("Secret deleted (soft) vault={Vault} secret={Secret} versions={Count}", vault, secretName, all.Count);
        SetRequestId(ctx);

        var latest = all.OrderByDescending(s => s.UpdatedOn).FirstOrDefault();
        if (latest is null) return Results.NotFound();

        // SecretClient.StartDeleteSecretAsync deserializes the response as a DeletedSecret —
        // an empty body (or plain 200 with no content) fails client-side JSON parsing.
        var now = DateTimeOffset.UtcNow;
        return Results.Ok(new
        {
            id = SecretUrl(secretName, latest.Version),
            attributes = Attributes(latest),
            recoveryId = $"{_baseUrl}/deletedsecrets/{secretName}",
            deletedDate = now.ToUnixTimeSeconds(),
            scheduledPurgeDate = now.AddDays(90).ToUnixTimeSeconds()
        });
    }

    #endregion

    #region Private helpers

    private static string SecretKey(string vault, string name, string version) =>
        $"keyvault/secrets/{vault}/{name}/{version}".ToLowerInvariant();

    // Deliberately omits the "/kv/{vault}" prefix used for routing incoming requests: the Key
    // Vault SDK's own KeyVaultIdentifier.Parse expects response "id" URLs shaped exactly
    // "{host}/secrets/{name}/{version}" (real Azure identifies the vault via a subdomain, not
    // a path segment) — an extra path segment here makes the SDK reject the response.
    private string SecretUrl(string name, string version) =>
        $"{_baseUrl}/secrets/{name}/{version}";

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();

    private static object Attributes(KeyVaultSecret s) => new
    {
        enabled = s.Enabled,
        created = s.CreatedOn.ToUnixTimeSeconds(),
        updated = s.UpdatedOn.ToUnixTimeSeconds(),
        expires = s.ExpiresOn?.ToUnixTimeSeconds()
    };

    private object SecretResponse(KeyVaultSecret secret) => new
    {
        value = secret.Value,
        id = SecretUrl(secret.Name, secret.Version),
        attributes = Attributes(secret)
    };

    #endregion
}
