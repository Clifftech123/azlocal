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
        _baseUrl = (config["AzLocal:BaseUrl"] ?? "http://localhost").TrimEnd('/');
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(KeyVaultRoutes.Secrets,       ListSecretsAsync);
        app.MapPut(KeyVaultRoutes.SecretByName,  SetSecretAsync);
        app.MapGet(KeyVaultRoutes.SecretByName,  GetSecretAsync);
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
            .Select(s => new { id = SecretUrl(vault, s.Name, s.Version), attributes = Attributes(s) });

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
        secret.Id = SecretUrl(vault, secretName, secret.Version);

        await _state.SetAsync(SecretKey(vault, secretName, secret.Version), secret);
        _logger.LogInformation("Secret set vault={Vault} secret={Secret} version={Version}", vault, secretName, secret.Version);

        SetRequestId(ctx);
        return Results.Ok(SecretResponse(vault, secret));
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
        return Results.Ok(SecretResponse(vault, secret));
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
        return Results.Ok(SecretResponse(vault, secret));
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
        return Results.Ok();
    }

    #endregion

    #region Private helpers

    private static string SecretKey(string vault, string name, string version) =>
        $"keyvault/secrets/{vault}/{name}/{version}".ToLowerInvariant();

    private string SecretUrl(string vault, string name, string version) =>
        $"{_baseUrl}/kv/{vault}/secrets/{name}/{version}";

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();

    private static object Attributes(KeyVaultSecret s) => new
    {
        enabled = s.Enabled,
        created = s.CreatedOn.ToUnixTimeSeconds(),
        updated = s.UpdatedOn.ToUnixTimeSeconds(),
        expires = s.ExpiresOn?.ToUnixTimeSeconds()
    };

    private static object SecretResponse(string vault, KeyVaultSecret secret) => new
    {
        value      = secret.Value,
        id         = SecretUrl(vault, secret.Name, secret.Version),
        attributes = Attributes(secret)
    };

    #endregion
}
