using AzLocal.Core.Interfaces;
using AzLocal.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzLocal.Services.ServiceBus;

public class ServiceBusQueueHandler : IServiceHandler
{
    private readonly IStateStore _state;
    private readonly ILogger<ServiceBusQueueHandler> _logger;

    public string ServiceName => "ServiceBus";

    public ServiceBusQueueHandler(IStateStore state, ILogger<ServiceBusQueueHandler> logger)
    {
        _state = state;
        _logger = logger;
    }

    public void MapRoutes(WebApplication app)
    {
        app.MapPost(ServiceBusRoutes.Messages, SendMessageAsync);
        app.MapGet(ServiceBusRoutes.Messages, PeekMessagesAsync);
        app.MapPost(ServiceBusRoutes.MessageHead, ReceiveMessageAsync);      // peek-lock
        app.MapDelete(ServiceBusRoutes.MessageHead, ReceiveAndDeleteAsync);  // destructive receive
        app.MapDelete(ServiceBusRoutes.MessageLock, CompleteMessageAsync);   // complete after peek-lock
        app.MapPost(ServiceBusRoutes.DeadLetter, DeadLetterMessageAsync);
    }

    #region Route handlers

    /// <summary>Enqueues a new message. Body is the raw message payload.</summary>
    private async Task<IResult> SendMessageAsync(string @namespace, string queue, HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();

        var message = new ServiceBusMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = body,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        // Carry any x-ms-prop-* headers as application properties.
        foreach (var header in ctx.Request.Headers.Where(h => h.Key.StartsWith("x-ms-prop-", StringComparison.OrdinalIgnoreCase)))
            message.ApplicationProperties[header.Key[10..]] = header.Value.ToString();

        await _state.SetAsync(ActiveKey(@namespace, queue, message.MessageId), message);
        _logger.LogInformation("Message sent ns={Namespace} queue={Queue} messageId={MessageId}", @namespace, queue, message.MessageId);

        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();
        ctx.Response.Headers["Content-Location"] = $"/sb/{@namespace}/{queue}/messages/{message.MessageId}";
        return Results.Created();
    }

    /// <summary>Returns up to 32 messages without removing them from the queue.</summary>
    private async Task<IResult> PeekMessagesAsync(string @namespace, string queue, HttpContext ctx)
    {
        var messages = await _state.ListAsync<ServiceBusMessage>(ActivePrefix(@namespace, queue));
        var page = messages.OrderBy(m => m.EnqueuedAt).Take(32).ToList();
        _logger.LogDebug("PeekMessages ns={Namespace} queue={Queue} count={Count}", @namespace, queue, page.Count);

        SetRequestId(ctx);
        return Results.Ok(page);
    }

    /// <summary>
    /// Receives the oldest message under a peek-lock. The caller must complete or dead-letter
    /// the message using the returned <c>LockToken</c> before the lock expires.
    /// Returns 204 when the queue is empty.
    /// </summary>
    private async Task<IResult> ReceiveMessageAsync(string @namespace, string queue, HttpContext ctx)
    {
        var messages = await _state.ListAsync<ServiceBusMessage>(ActivePrefix(@namespace, queue));
        var message = messages.OrderBy(m => m.EnqueuedAt).FirstOrDefault();
        if (message is null)
        {
            _logger.LogDebug("ReceiveMessage ns={Namespace} queue={Queue} — queue empty", @namespace, queue);
            return Results.NoContent();
        }

        message.DeliveryCount++;
        await _state.SetAsync(ActiveKey(@namespace, queue, message.MessageId), message);

        var lockToken = Guid.NewGuid().ToString();
        await _state.SetAsync(LockKey(@namespace, queue, lockToken), message.MessageId);

        _logger.LogInformation("Message received (peek-lock) ns={Namespace} queue={Queue} messageId={MessageId} lock={Lock}",
            @namespace, queue, message.MessageId, lockToken);

        SetRequestId(ctx);
        ctx.Response.Headers["BrokerProperties"] = JsonSerializer.Serialize(new
        {
            MessageId = message.MessageId,
            LockToken = lockToken,
            DeliveryCount = message.DeliveryCount,
            EnqueuedTimeUtc = message.EnqueuedAt
        });
        return Results.Ok(message.Body);
    }

    /// <summary>
    /// Receives and immediately deletes the oldest message (no lock required to complete).
    /// Returns 204 when the queue is empty.
    /// </summary>
    private async Task<IResult> ReceiveAndDeleteAsync(string @namespace, string queue, HttpContext ctx)
    {
        var messages = await _state.ListAsync<ServiceBusMessage>(ActivePrefix(@namespace, queue));
        var message = messages.OrderBy(m => m.EnqueuedAt).FirstOrDefault();
        if (message is null) return Results.NoContent();

        await _state.DeleteAsync(ActiveKey(@namespace, queue, message.MessageId));
        _logger.LogInformation("Message receive-deleted ns={Namespace} queue={Queue} messageId={MessageId}", @namespace, queue, message.MessageId);

        SetRequestId(ctx);
        ctx.Response.Headers["BrokerProperties"] = JsonSerializer.Serialize(new
        {
            MessageId = message.MessageId,
            DeliveryCount = message.DeliveryCount + 1,
            EnqueuedTimeUtc = message.EnqueuedAt
        });
        return Results.Ok(message.Body);
    }

    /// <summary>Completes a previously peek-locked message, removing it from the queue.</summary>
    private async Task<IResult> CompleteMessageAsync(string @namespace, string queue, string lockToken, HttpContext ctx)
    {
        var messageId = await _state.GetAsync<string>(LockKey(@namespace, queue, lockToken));
        if (messageId is null)
        {
            _logger.LogWarning("CompleteMessage lock not found ns={Namespace} queue={Queue} lock={Lock}", @namespace, queue, lockToken);
            return Results.NotFound();
        }

        await _state.DeleteAsync(ActiveKey(@namespace, queue, messageId));
        await _state.DeleteAsync(LockKey(@namespace, queue, lockToken));
        _logger.LogInformation("Message completed ns={Namespace} queue={Queue} messageId={MessageId}", @namespace, queue, messageId);

        SetRequestId(ctx);
        return Results.Ok();
    }

    /// <summary>Moves a peek-locked message to the dead-letter queue.</summary>
    private async Task<IResult> DeadLetterMessageAsync(string @namespace, string queue, string lockToken, HttpContext ctx)
    {
        var messageId = await _state.GetAsync<string>(LockKey(@namespace, queue, lockToken));
        if (messageId is null)
        {
            _logger.LogWarning("DeadLetter lock not found ns={Namespace} queue={Queue} lock={Lock}", @namespace, queue, lockToken);
            return Results.NotFound();
        }

        var message = await _state.GetAsync<ServiceBusMessage>(ActiveKey(@namespace, queue, messageId));
        if (message is not null)
        {
            string? reason = null;
            try
            {
                using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
                doc.RootElement.TryGetProperty("deadLetterReason", out var prop);
                reason = prop.GetString();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "DeadLetter could not parse reason body — continuing without reason");
            }

            message.DeadLetterReason = reason;
            await _state.SetAsync(DeadLetterKey(@namespace, queue, messageId), message);
            await _state.DeleteAsync(ActiveKey(@namespace, queue, messageId));
        }

        await _state.DeleteAsync(LockKey(@namespace, queue, lockToken));
        _logger.LogInformation("Message dead-lettered ns={Namespace} queue={Queue} messageId={MessageId}", @namespace, queue, messageId);

        SetRequestId(ctx);
        return Results.Ok();
    }

    #endregion

    #region Private helpers

    private static string ActivePrefix(string ns, string queue) =>
        $"servicebus/{ns}/{queue}/active/".ToLowerInvariant();

    private static string ActiveKey(string ns, string queue, string messageId) =>
        $"servicebus/{ns}/{queue}/active/{messageId}".ToLowerInvariant();

    private static string LockKey(string ns, string queue, string lockToken) =>
        $"servicebus/{ns}/{queue}/locks/{lockToken}".ToLowerInvariant();

    private static string DeadLetterKey(string ns, string queue, string messageId) =>
        $"servicebus/{ns}/{queue}/deadletter/{messageId}".ToLowerInvariant();

    private static void SetRequestId(HttpContext ctx) =>
        ctx.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();

    #endregion
}
