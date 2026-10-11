using Azure.Messaging.ServiceBus;

namespace AzLocal.CompatTests;

public class ServiceBusSdkTests : IClassFixture<CompatFixture>
{
    private readonly CompatFixture _host;
    public ServiceBusSdkTests(CompatFixture host) => _host = host;

    [Fact]
    public async Task OfficialSdkSendsPeeksReceivesAndCompletes()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var client = _host.Clients.CreateServiceBusClient(_host.AmqpPort);
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        await using var sender = client.CreateSender(queue);
        await using var receiver = client.CreateReceiver(queue);
        var sent = new ServiceBusMessage("hello") { MessageId = "message-one", ContentType = "text/plain" };
        sent.ApplicationProperties["number"] = 42;
        await sender.SendMessageAsync(sent, ct);
        var peeked = await receiver.PeekMessageAsync(cancellationToken: ct);
        Assert.NotNull(peeked);
        Assert.Equal("hello", peeked.Body.ToString());
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(received);
        Assert.Equal("message-one", received.MessageId);
        Assert.Equal("hello", received.Body.ToString());
        Assert.Equal(42, received.ApplicationProperties["number"]);
        Assert.NotEqual(Guid.Empty.ToString(), received.LockToken);
        Assert.True(received.LockedUntil > DateTimeOffset.UtcNow);
        var beforeRenewal = received.LockedUntil;
        await receiver.RenewMessageLockAsync(received, ct);
        Assert.True(received.LockedUntil >= beforeRenewal);
        await receiver.CompleteMessageAsync(received, ct);
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(200), ct));
    }

    [Fact]
    public async Task OfficialSdkAbandonsAndDeadLetters()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var client = _host.Clients.CreateServiceBusClient(_host.AmqpPort);
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        await using var sender = client.CreateSender(queue);
        await using var receiver = client.CreateReceiver(queue);
        await sender.SendMessageAsync(new ServiceBusMessage("retry"), ct);
        var first = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(first);
        await receiver.AbandonMessageAsync(first, cancellationToken: ct);
        var second = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(second);
        Assert.Equal(2, second.DeliveryCount);
        await receiver.DeadLetterMessageAsync(second, "test-reason", cancellationToken: ct);
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(200), ct));
        await using var deadLetters = client.CreateReceiver(queue, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var deadLetter = await deadLetters.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(deadLetter);
        Assert.Equal("retry", deadLetter.Body.ToString());
        Assert.Equal("test-reason", deadLetter.DeadLetterReason);
        await deadLetters.CompleteMessageAsync(deadLetter, ct);
    }

    [Fact]
    public async Task OfficialSdkSendsBatchesAndReceivesAndDeletes()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var client = _host.Clients.CreateServiceBusClient(_host.AmqpPort);
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        await using var sender = client.CreateSender(queue);
        await sender.SendMessagesAsync(new[] { new ServiceBusMessage("one"), new ServiceBusMessage("two") }, ct);
        using var batch = await sender.CreateMessageBatchAsync(ct);
        Assert.True(batch.TryAddMessage(new ServiceBusMessage("three")));
        Assert.True(batch.TryAddMessage(new ServiceBusMessage("four")));
        await sender.SendMessagesAsync(batch, ct);
        await using var receiver = client.CreateReceiver(queue, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        var bodies = new List<string>();
        for (var index = 0; index < 4; index++)
        {
            var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
            Assert.NotNull(received);
            bodies.Add(received.Body.ToString());
        }
        Assert.Equal(new[] { "one", "two", "three", "four" }, bodies);
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(200), ct));
    }

    [Fact]
    public async Task ConcurrentSdkReceiversCannotClaimTheSameMessage()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var client = _host.Clients.CreateServiceBusClient(_host.AmqpPort);
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        await using var sender = client.CreateSender(queue);
        await sender.SendMessageAsync(new ServiceBusMessage("one"), ct);
        await using var first = client.CreateReceiver(queue);
        await using var second = client.CreateReceiver(queue);
        var results = await Task.WhenAll(first.ReceiveMessageAsync(TimeSpan.FromMilliseconds(250), ct), second.ReceiveMessageAsync(TimeSpan.FromMilliseconds(250), ct));
        Assert.Single(results, message => message is not null);
    }

    [Fact]
    public async Task ExpiredSdkLocksRejectCompletionAndRedeliver()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var client = _host.Clients.CreateServiceBusClient(_host.AmqpPort);
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        await using var sender = client.CreateSender(queue);
        await using var receiver = client.CreateReceiver(queue);
        await sender.SendMessageAsync(new ServiceBusMessage("one"), ct);
        var first = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(first);
        var state = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<AzLocal.Core.Interfaces.IStateStore>(_host.Services);
        var prefix = $"servicebus/local/{queue}/active/";
        var stored = Assert.Single(await state.ListAsync<AzLocal.Core.Models.ServiceBusMessage>(prefix));
        stored.LockedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);
        await state.SetAsync(prefix + stored.MessageId, stored);
        var error = await Assert.ThrowsAsync<ServiceBusException>(() => receiver.CompleteMessageAsync(first, ct));
        Assert.Equal(ServiceBusFailureReason.MessageLockLost, error.Reason);
        var second = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(second);
        Assert.Equal(2, second.DeliveryCount);
        Assert.NotEqual(first.LockToken, second.LockToken);
        await receiver.CompleteMessageAsync(second, ct);
    }

    [Fact]
    public async Task HttpAndAmqpShareLocalQueues()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        var clients = _host.Clients;
        using var http = clients.CreateServiceBusHttpClient("local");
        var queue = "sdk-" + Guid.NewGuid().ToString("N");
        using var sent = await http.PostAsync($"{queue}/messages", new StringContent("from-http"), ct);
        sent.EnsureSuccessStatusCode();
        await using var client = clients.CreateServiceBusClient(_host.AmqpPort);
        await using var receiver = client.CreateReceiver(queue);
        Assert.Equal("from-http", (await receiver.PeekMessageAsync(cancellationToken: ct)).Body.ToString());
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), ct);
        Assert.NotNull(received);
        Assert.Equal("from-http", received.Body.ToString());
        await receiver.CompleteMessageAsync(received, ct);
        await using var sender = client.CreateSender(queue);
        await sender.SendMessageAsync(new ServiceBusMessage("from-amqp"), ct);
        using var fromAmqp = await http.DeleteAsync($"{queue}/messages/head", ct);
        fromAmqp.EnsureSuccessStatusCode();
        Assert.Equal("from-amqp", await fromAmqp.Content.ReadAsStringAsync(ct));
    }
}
