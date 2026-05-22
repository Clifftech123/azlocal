using AzLocal.Core.Interfaces;
using AzLocal.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;

namespace AzLocal.Services.BlobStorage;

public class BlobServiceHandler : IServiceHandler
{
    private readonly IStateStore _state;
    private readonly IBlobFileStore _blobs;
    private readonly ILogger<BlobServiceHandler> _logger;
    private readonly string _baseUrl;

    public string ServiceName => "BlobStorage";

    public BlobServiceHandler(IStateStore state, IBlobFileStore blobs, ILogger<BlobServiceHandler> logger, IConfiguration config)
    {
        _state = state;
        _blobs = blobs;
        _logger = logger;
        _baseUrl = (config["AzLocal:BaseUrl"] ?? "http://localhost").TrimEnd('/');
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapGet(BlobRoutes.ListContainers, ListContainersAsync);
        app.MapGet(BlobRoutes.Container,      ListBlobsAsync);
        app.MapPut(BlobRoutes.Container,      CreateContainerAsync);
        app.MapDelete(BlobRoutes.Container,   DeleteContainerAsync);
        app.MapPut(BlobRoutes.BlobItem,       UploadBlobAsync);
        app.MapGet(BlobRoutes.BlobItem,       DownloadBlobAsync);
        app.MapDelete(BlobRoutes.BlobItem,    DeleteBlobAsync);
        app.MapMethods(BlobRoutes.BlobItem, ["HEAD"], GetBlobPropertiesAsync);
    }

    #region Container handlers

    private async Task<IResult> ListContainersAsync(string account, HttpContext ctx)
    {
        var containers = await _state.ListAsync<BlobContainer>($"blob/containers/{account}/");
        _logger.LogDebug("ListContainers account={Account} count={Count}", account, containers.Count);

        var xml = BuildXml(writer =>
        {
            writer.WriteStartElement("EnumerationResults");
            writer.WriteAttributeString("ServiceEndpoint", $"{_baseUrl}/azu/{account}");
            writer.WriteStartElement("Containers");
            foreach (var c in containers)
            {
                writer.WriteStartElement("Container");
                writer.WriteElementString("Name", c.Name);
                writer.WriteStartElement("Properties");
                writer.WriteElementString("Last-Modified", c.CreatedOn.ToString("R"));
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteElementString("NextMarker", string.Empty);
            writer.WriteEndElement();
        });

        SetRequestId(ctx);
        return Results.Content(xml, "application/xml");
    }

    private async Task<IResult> ListBlobsAsync(string account, string container, HttpContext ctx)
    {
        var blobs = await _state.ListAsync<BlobItem>($"blob/items/{account}/{container}/");
        _logger.LogDebug("ListBlobs account={Account} container={Container} count={Count}", account, container, blobs.Count);

        var xml = BuildXml(writer =>
        {
            writer.WriteStartElement("EnumerationResults");
            writer.WriteAttributeString("ServiceEndpoint", $"{_baseUrl}/azu/{account}");
            writer.WriteAttributeString("ContainerName", container);
            writer.WriteStartElement("Blobs");
            foreach (var b in blobs)
            {
                writer.WriteStartElement("Blob");
                writer.WriteElementString("Name", b.Name);
                writer.WriteStartElement("Properties");
                writer.WriteElementString("Last-Modified", b.LastModified.ToString("R"));
                writer.WriteElementString("Etag", $"\"{b.ETag}\"");
                writer.WriteElementString("Content-Length", b.SizeBytes.ToString());
                writer.WriteElementString("Content-Type", b.ContentType);
                writer.WriteElementString("BlobType", b.BlobType);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteElementString("NextMarker", string.Empty);
            writer.WriteEndElement();
        });

        SetRequestId(ctx);
        return Results.Content(xml, "application/xml");
    }

    private async Task<IResult> CreateContainerAsync(string account, string container, HttpContext ctx)
    {
        var key = ContainerKey(account, container);
        if (await _state.ExistsAsync(key))
        {
            _logger.LogWarning("CreateContainer conflict account={Account} container={Container}", account, container);
            return Results.Conflict();
        }

        await _state.SetAsync(key, new BlobContainer { Name = container, Location = "local" });
        _logger.LogInformation("Container created account={Account} container={Container}", account, container);
        SetRequestId(ctx);
        return Results.Created();
    }

    private async Task<IResult> DeleteContainerAsync(string account, string container, HttpContext ctx)
    {
        await _state.DeleteAsync(ContainerKey(account, container));

        var blobs = await _state.ListAsync<BlobItem>($"blob/items/{account}/{container}/");
        foreach (var b in blobs)
            await _state.DeleteAsync(BlobKey(account, container, b.Name));

        await _blobs.DeleteContainerAsync(container);
        _logger.LogInformation("Container deleted account={Account} container={Container} blobsRemoved={Count}", account, container, blobs.Count);
        SetRequestId(ctx);
        return Results.Accepted();
    }

    #endregion

    #region Blob handlers

    private async Task<IResult> UploadBlobAsync(string account, string container, string blobName, HttpContext ctx)
    {
        var contentType = ctx.Request.ContentType ?? "application/octet-stream";
        await _blobs.WriteAsync(container, blobName, ctx.Request.Body, contentType);

        var etag = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        await _state.SetAsync(BlobKey(account, container, blobName), new BlobItem
        {
            Name = blobName,
            ContainerName = container,
            ContentType = contentType,
            SizeBytes = ctx.Request.ContentLength ?? 0,
            ETag = etag,
            LastModified = now
        });

        _logger.LogInformation("Blob uploaded account={Account} container={Container} blob={Blob} contentType={ContentType}",
            account, container, blobName, contentType);

        ctx.Response.Headers["ETag"] = $"\"{etag}\"";
        ctx.Response.Headers["Last-Modified"] = now.ToString("R");
        SetRequestId(ctx);
        return Results.Created();
    }

    private async Task<IResult> DownloadBlobAsync(string account, string container, string blobName, HttpContext ctx)
    {
        var meta = await _state.GetAsync<BlobItem>(BlobKey(account, container, blobName));
        if (meta is null)
        {
            _logger.LogWarning("Blob not found account={Account} container={Container} blob={Blob}", account, container, blobName);
            return Results.NotFound();
        }

        Stream stream;
        try
        {
            stream = await _blobs.ReadAsync(container, blobName);
        }
        catch (FileNotFoundException ex)
        {
            // Metadata exists but the file was deleted out-of-band — treat as not found.
            _logger.LogWarning(ex, "Blob file missing despite metadata account={Account} container={Container} blob={Blob}", account, container, blobName);
            return Results.NotFound();
        }

        ctx.Response.Headers["ETag"] = $"\"{meta.ETag}\"";
        ctx.Response.Headers["Last-Modified"] = meta.LastModified.ToString("R");
        SetRequestId(ctx);
        return Results.Stream(stream, meta.ContentType);
    }

    private async Task<IResult> DeleteBlobAsync(string account, string container, string blobName, HttpContext ctx)
    {
        await _state.DeleteAsync(BlobKey(account, container, blobName));
        await _blobs.DeleteAsync(container, blobName);
        _logger.LogInformation("Blob deleted account={Account} container={Container} blob={Blob}", account, container, blobName);
        SetRequestId(ctx);
        return Results.Accepted();
    }

    private async Task<IResult> GetBlobPropertiesAsync(string account, string container, string blobName, HttpContext ctx)
    {
        var meta = await _state.GetAsync<BlobItem>(BlobKey(account, container, blobName));
        if (meta is null) return Results.NotFound();

        ctx.Response.Headers["ETag"] = $"\"{meta.ETag}\"";
        ctx.Response.Headers["Last-Modified"] = meta.LastModified.ToString("R");
        ctx.Response.Headers["Content-Type"] = meta.ContentType;
        ctx.Response.Headers["Content-Length"] = meta.SizeBytes.ToString();
        ctx.Response.Headers["x-ms-blob-type"] = meta.BlobType;
        SetRequestId(ctx);
        return Results.Ok();
    }

    #endregion

    #region Private helpers

    private static string ContainerKey(string account, string container) =>
        $"blob/containers/{account}/{container}".ToLowerInvariant();

    private static string BlobKey(string account, string container, string blob) =>
        $"blob/items/{account}/{container}/{blob}".ToLowerInvariant();

    // x-ms-request-id is checked by the Azure SDK to correlate requests in logs.
    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();

    // Uses XmlWriter so blob/container names with <, >, & are safely escaped.
    private static string BuildXml(Action<XmlWriter> build)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        build(writer);
        writer.WriteEndDocument();
        return sb.ToString();
    }

    #endregion
}
