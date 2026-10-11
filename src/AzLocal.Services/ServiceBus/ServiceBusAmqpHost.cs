using System.Collections.Concurrent;
using System.Text;
using Amqp;
using Amqp.Framing;
using Amqp.Handler;
using Amqp.Listener;
using Amqp.Types;
using AzLocal.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EventId = Amqp.Handler.EventId;
using StoredMessage = AzLocal.Core.Models.ServiceBusMessage;

namespace AzLocal.Services.ServiceBus;

/// <summary>Local AMQP queue listener for the official Service Bus SDK.</summary>
public sealed class ServiceBusAmqpHost : IHostedService
{
    private readonly IStateStore _state;
    private readonly ServiceBusCoordinator _coordinator;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceBusAmqpHost> _logger;
    private ContainerHost? _host;
    private readonly ConcurrentDictionary<string, Lazy<bool>> _registered = new();

    public ServiceBusAmqpHost(IStateStore state, ServiceBusCoordinator coordinator, IConfiguration config, ILogger<ServiceBusAmqpHost> logger)
    {
        _state = state;
        _coordinator = coordinator;
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(_config["AzLocal:AmqpEnabled"], "false", StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;
        var port = 5672;
        if (_config["AzLocal:AmqpPort"] is { Length: > 0 } portValue && !int.TryParse(portValue, out port))
            throw new InvalidOperationException("AzLocal:AmqpPort must be an integer.");
        if (port is < 1 or > 65535) throw new InvalidOperationException("AzLocal:AmqpPort must be between 1 and 65535.");
        var host = new ContainerHost(new Address($"amqp://127.0.0.1:{port}"));
        _host = host;
        foreach (var listener in host.Listeners)
        {
            listener.SASL.EnableAnonymousMechanism = true;
            listener.SASL.EnableMechanism(new Symbol("MSSBCBS"), new LocalSasl());
            listener.HandlerFactory = _ => new DeliveryHandler();
        }
        host.AddressResolver = (container, attach) =>
        {
            if (attach.Target is not Target || attach.Source is not Source source)
                throw new AmqpException(ErrorCode.NotImplemented, "Transaction coordinator links are not supported.");
            if (source.FilterSet?.ContainsKey(new Symbol("com.microsoft:session-filter")) == true)
                throw new AmqpException(ErrorCode.NotImplemented, "Session queues are not supported.");
            var address = attach.Role ? source.Address : ((Target)attach.Target).Address;
            if (string.IsNullOrEmpty(address)) return address!;
            _logger.LogDebug("AMQP attach role={Role} address={Address} target={Target} source={Source}", attach.Role, address, attach.Target, attach.Source);
            var path = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Trim('/') : address.Trim('/');
            _ = _registered.GetOrAdd(path, key => new Lazy<bool>(() =>
            {
                if (key == "$cbs" || key.EndsWith("/$management", StringComparison.Ordinal))
                    host.RegisterRequestProcessor(key, new Management(this, key));
                else
                {
                    var queue = new QueueEndpoint(this, key);
                    host.RegisterMessageProcessor(key, queue);
                    host.RegisterMessageSource(key, queue);
                }
                return true;
            })).Value;
            return path;
        };
        host.Open();
        _logger.LogInformation("Local Service Bus AMQP listener on 127.0.0.1:{Port}", port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _host?.Close();
        _host = null;
        _registered.Clear();
        return Task.CompletedTask;
    }

    private const string DeadLetterSuffix = "/$DeadLetterQueue";
    private static string Prefix(string queue) => queue.EndsWith(DeadLetterSuffix, StringComparison.OrdinalIgnoreCase)
        ? $"servicebus/local/{queue[..^DeadLetterSuffix.Length]}/deadletter/".ToLowerInvariant()
        : $"servicebus/local/{queue}/active/".ToLowerInvariant();
    private static string Key(string queue, string id) => Prefix(queue) + id;
    private async Task<T> Serialized<T>(Func<Task<T>> action)
    {
        await _coordinator.Gate.WaitAsync();
        try { return await action(); }
        finally { _coordinator.Gate.Release(); }
    }

    private static Message Decode(byte[] bytes) => Message.Decode(new ByteBuffer(bytes, 0, bytes.Length, bytes.Length));

    private Task<bool> Enqueue(string queue, Message transfer) => Serialized(async () =>
    {
        Message[] messages;
        if (transfer.Format == 0) messages = [transfer];
        else if (transfer.Format == 0x80013700)
            messages = transfer.Body switch
            {
                Data[] sections => sections.Select(section => Decode(section.Binary)).ToArray(),
                byte[] bytes => [Decode(bytes)],
                _ => throw new AmqpException(ErrorCode.DecodeError, "Invalid batch payload.")
            };
        else throw new AmqpException(ErrorCode.NotImplemented, "Unknown message format.");
        var buffers = messages.Select(message => message.Encode()).ToArray();
        if (transfer.Encode().Length > 256 * 1024)
            throw new AmqpException(new Symbol("amqp:link:message-size-exceeded"), "The local message limit is 256 KiB.");
        var sequenceKey = $"servicebus/local/{queue}/sequence".ToLowerInvariant();
        var sequence = await _state.GetAsync<long>(sequenceKey);
        for (var index = 0; index < messages.Length; index++)
        {
            var message = messages[index];
            var buffer = buffers[index];
            var record = new StoredMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = message.Body is byte[] bytes ? Encoding.UTF8.GetString(bytes) : message.Body?.ToString() ?? "",
                AmqpData = Convert.ToBase64String(buffer.Buffer, buffer.Offset, buffer.Length),
                SequenceNumber = ++sequence,
                EnqueuedAt = DateTimeOffset.UtcNow
            };
            await _state.SetAsync(Key(queue, record.MessageId), record);
        }
        await _state.SetAsync(sequenceKey, sequence);
        return true;
    });

    private Task<StoredMessage?> Claim(string queue, bool destructive) => Serialized(async () =>
    {
        var records = await _state.ListAsync<StoredMessage>(Prefix(queue));
        var record = records.Where(m => m.LockedUntil is null || m.LockedUntil <= DateTimeOffset.UtcNow)
            .OrderBy(m => m.EnqueuedAt).FirstOrDefault();
        if (record is null) return null;
        record.DeliveryCount++;
        record.LockToken = Guid.NewGuid().ToString();
        record.LockedUntil = DateTimeOffset.UtcNow.AddSeconds(30);
        if (destructive) await _state.DeleteAsync(Key(queue, record.MessageId));
        else await _state.SetAsync(Key(queue, record.MessageId), record);
        return record;
    });

    private Task<bool> Settle(string queue, string token, DeliveryState outcome) => Serialized(async () =>
    {
        var records = await _state.ListAsync<StoredMessage>(Prefix(queue));
        var record = records.FirstOrDefault(m => m.LockToken == token && m.LockedUntil > DateTimeOffset.UtcNow);
        if (record is null) return false;
        if (outcome is Accepted) await _state.DeleteAsync(Key(queue, record.MessageId));
        else if (outcome is Rejected rejected)
        {
            record.DeadLetterReason = rejected.Error?.Info?[new Symbol("DeadLetterReason")] as string ?? rejected.Error?.Description;
            record.LockToken = null;
            record.LockedUntil = null;
            await _state.SetAsync($"servicebus/local/{queue}/deadletter/{record.MessageId}".ToLowerInvariant(), record);
            await _state.DeleteAsync(Key(queue, record.MessageId));
        }
        else if (outcome is Released or Modified)
        {
            if (outcome is Modified { UndeliverableHere: true })
                throw new AmqpException(ErrorCode.NotImplemented, "Message deferral is not supported.");
            record.LockToken = null;
            record.LockedUntil = null;
            await _state.SetAsync(Key(queue, record.MessageId), record);
        }
        else return false;
        return true;
    });

    private Task<DateTime[]> Renew(string queue, Guid[] tokens) => Serialized(async () =>
    {
        var records = await _state.ListAsync<StoredMessage>(Prefix(queue));
        var locked = tokens.Select(token => records.FirstOrDefault(m => m.LockToken == token.ToString() && m.LockedUntil > DateTimeOffset.UtcNow)).ToArray();
        if (locked.Any(record => record is null)) throw new AmqpException(new Symbol("com.microsoft:message-lock-lost"), "The message lock has expired.");
        var expiry = DateTimeOffset.UtcNow.AddSeconds(30);
        foreach (var record in locked)
        {
            record!.LockedUntil = expiry;
            await _state.SetAsync(Key(queue, record.MessageId), record);
        }
        return tokens.Select(_ => expiry.UtcDateTime).ToArray();
    });

    private static Message WireMessage(StoredMessage record)
    {
        var message = record.AmqpData is { } data ? Decode(Convert.FromBase64String(data))
            : new Message { BodySection = new Data { Binary = Encoding.UTF8.GetBytes(record.Body) }, Properties = new Properties { MessageId = record.MessageId } };
        message.ApplicationProperties ??= new ApplicationProperties();
        foreach (var property in record.ApplicationProperties) message.ApplicationProperties.Map[property.Key] = property.Value;
        if (record.DeadLetterReason is { } reason) message.ApplicationProperties.Map["DeadLetterReason"] = reason;
        message.Header ??= new Header();
        message.Header.DeliveryCount = (uint)Math.Max(0, record.DeliveryCount - 1);
        message.MessageAnnotations ??= new MessageAnnotations();
        message.MessageAnnotations.Map[new Symbol("x-opt-enqueued-time")] = record.EnqueuedAt.UtcDateTime;
        message.MessageAnnotations.Map[new Symbol("x-opt-sequence-number")] = record.SequenceNumber;
        if (record.LockedUntil is { } until) message.MessageAnnotations.Map[new Symbol("x-opt-locked-until")] = until.UtcDateTime;
        if (record.LockToken is { } token) message.MessageAnnotations.Map[new Symbol("azlocal-lock-token")] = token;
        return message;
    }

    private sealed class LocalSasl() : Amqp.Sasl.SaslProfile(new Symbol("MSSBCBS"))
    {
        protected override ITransport UpgradeTransport(ITransport transport) => transport;
        protected override DescribedList GetStartCommand(string hostname) => new Amqp.Sasl.SaslMechanisms { SaslServerMechanisms = [new Symbol("MSSBCBS")] };
        protected override DescribedList OnCommand(DescribedList command) => new Amqp.Sasl.SaslOutcome { Code = Amqp.Sasl.SaslCode.Ok };
    }

    private sealed class DeliveryHandler : IHandler
    {
        public bool CanHandle(EventId id) => id is EventId.SendDelivery or EventId.LinkLocalOpen;
        public void Handle(Event value)
        {
            if (value.Id == EventId.LinkLocalOpen)
            {
                ((Attach)value.Context).MaxMessageSize = 256 * 1024;
                return;
            }
            var delivery = (IDelivery)value.Context;
            var token = delivery.Message.MessageAnnotations?.Map[new Symbol("azlocal-lock-token")] as string;
            if (token is not null) delivery.Tag = Guid.Parse(token).ToByteArray();
        }
    }

    private sealed class QueueEndpoint(ServiceBusAmqpHost owner, string queue) : IMessageProcessor, IMessageSource
    {
        public int Credit => 100;
        public void Process(MessageContext context)
        {
            try { owner.Enqueue(queue, context.Message).GetAwaiter().GetResult(); context.Complete(); }
            catch (Exception ex) { owner._logger.LogWarning(ex, "AMQP send failed"); context.Complete(ex is AmqpException amqp ? amqp.Error : new Error(ErrorCode.InternalError) { Description = "Could not store the message." }); }
        }
        public async Task<ReceiveContext> GetMessageAsync(ListenerLink link)
        {
            try
            {
                // SettleOnSend identifies receive-and-delete links.
                var record = await owner.Claim(queue, link.SettleOnSend);
                if (record is not null) return new ReceiveContext(link, WireMessage(record)) { UserToken = record.LockToken };
                await Task.Delay(50);
            }
            catch (Exception ex) { owner._logger.LogWarning(ex, "AMQP receive failed"); }
            return null!;
        }
        public void DisposeMessage(ReceiveContext received, DispositionContext disposition)
        {
            try
            {
                var settled = owner.Settle(queue, (string)received.UserToken, disposition.DeliveryState).GetAwaiter().GetResult();
                disposition.Complete(settled ? null : new Error(new Symbol("com.microsoft:message-lock-lost")));
            }
            catch (Exception ex) { owner._logger.LogWarning(ex, "AMQP settlement failed"); disposition.Complete(ex is AmqpException amqp ? amqp.Error : new Error(ErrorCode.InternalError)); }
        }
    }

    private sealed class Management(ServiceBusAmqpHost owner, string address) : IRequestProcessor
    {
        public int Credit => 100;
        public void Process(RequestContext context)
        {
            try
            {
                owner._logger.LogDebug("AMQP management address={Address} operation={Operation}", address, context.Message.ApplicationProperties?.Map["operation"]);
                var status = 200;
                Map body = new();
                if (address != "$cbs")
                {
                    var operation = context.Message.ApplicationProperties?.Map["operation"] as string;
                    var queue = address[..^"/$management".Length];
                    if (operation == "com.microsoft:peek-message")
                    {
                        var request = context.Message.Body as Map;
                        var count = request?["message-count"] is int amount ? amount : 1;
                        var from = request?["from-sequence-number"] is long sequence ? sequence : 0;
                        var records = owner.Serialized(() => owner._state.ListAsync<StoredMessage>(Prefix(queue))).GetAwaiter().GetResult();
                        var messages = records.Where(m => m.SequenceNumber >= from).OrderBy(m => m.EnqueuedAt).Take(count)
                            .Select(m => { var encoded = WireMessage(m).Encode(); return new Map { ["message"] = encoded.Buffer.AsSpan(encoded.Offset, encoded.Length).ToArray() }; }).ToArray();
                        var messageList = new Amqp.Types.List();
                        foreach (var message in messages) messageList.Add(message);
                        body["messages"] = messageList;
                        if (messages.Length == 0) status = 204;
                    }
                    else if (operation == "com.microsoft:renew-lock")
                    {
                        var request = context.Message.Body as Map;
                        var tokens = ((System.Collections.IEnumerable)request!["lock-tokens"]).Cast<Guid>().ToArray();
                        body["expirations"] = owner.Renew(queue, tokens).GetAwaiter().GetResult();
                    }
                    else status = 501;
                }
                context.Complete(new Message(body)
                {
                    Properties = new Properties { CorrelationId = context.Message.Properties.MessageId },
                    ApplicationProperties = new ApplicationProperties { Map = { [address == "$cbs" ? "status-code" : "statusCode"] = status, [address == "$cbs" ? "status-description" : "statusDescription"] = status == 501 ? "Operation not implemented" : "OK" } }
                });
            }
            catch (Exception ex) { owner._logger.LogWarning(ex, "AMQP management request failed"); context.Complete(new Message { Properties = new Properties { CorrelationId = context.Message.Properties.MessageId }, ApplicationProperties = new ApplicationProperties { Map = { [address == "$cbs" ? "status-code" : "statusCode"] = 500 } } }); }
        }
    }
}
