using AzLocal.IntegrationTests.Fixtures;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AzLocal.IntegrationTests;

// These tests exercise the HTTP queue endpoints. Official SDK coverage is in AzLocal.CompatTests.
public class ServiceBusTests : IClassFixture<EmulatorFixture>
{
    private readonly EmulatorFixture _fixture;

    public ServiceBusTests(EmulatorFixture fixture) => _fixture = fixture;

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task ConcurrentReceiversClaimMessageOnlyOnce()
    {
        using var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("one"));
        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => client.PostAsync($"{queue}/messages/head", null)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task ExpiredLockRedeliversAndCannotSettleNewDelivery()
    {
        var ns = UniqueName("ns");
        var queue = UniqueName("queue");
        using var client = NewClient(ns);
        await client.PostAsync($"{queue}/messages", new StringContent("one"));
        var first = await client.PostAsync($"{queue}/messages/head", null);
        var oldToken = GetLockToken(first);
        var state = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<AzLocal.Core.Interfaces.IStateStore>(_fixture.Services);
        var prefix = $"servicebus/{ns}/{queue}/active/".ToLowerInvariant();
        var message = Assert.Single(await state.ListAsync<AzLocal.Core.Models.ServiceBusMessage>(prefix));
        message.LockedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);
        await state.SetAsync(prefix + message.MessageId, message);
        var expiredComplete = await client.DeleteAsync($"{queue}/messages/{oldToken}");
        Assert.Equal(HttpStatusCode.NotFound, expiredComplete.StatusCode);
        var second = await client.PostAsync($"{queue}/messages/head", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(oldToken, GetLockToken(second));
        using var properties = JsonDocument.Parse(second.Headers.GetValues("BrokerProperties").Single());
        Assert.Equal(2, properties.RootElement.GetProperty("DeliveryCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{queue}/messages/{oldToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{queue}/messages/{GetLockToken(second)}")).StatusCode);
    }

    private HttpClient NewClient(string @namespace) => _fixture.Clients.CreateServiceBusHttpClient(@namespace);

    private static string? GetLockToken(HttpResponseMessage response)
    {
        var brokerProps = response.Headers.GetValues("BrokerProperties").First();
        using var doc = JsonDocument.Parse(brokerProps);
        return doc.RootElement.GetProperty("LockToken").GetString();
    }

    [Fact]
    public async Task SendMessage_ReturnsCreatedWithContentLocation()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");

        var response = await client.PostAsync($"{queue}/messages", new StringContent("hello"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains($"/{queue}/messages/", response.Content.Headers.GetValues("Content-Location").First());
    }

    [Fact]
    public async Task PeekMessages_ReturnsSentMessage_WithoutRemovingIt()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("peek-me"));

        var response = await client.GetAsync($"{queue}/messages");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(messages);
        Assert.Equal("peek-me", messages[0].GetProperty("body").GetString());

        // Peeking must not consume the message — it should still be there.
        var secondPeek = await client.GetAsync($"{queue}/messages");
        using var doc2 = JsonDocument.Parse(await secondPeek.Content.ReadAsStringAsync());
        Assert.Single(doc2.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ReceiveMessage_WithPeekLock_ThenComplete_RemovesMessage()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("lock-me"));

        var received = await client.PostAsync($"{queue}/messages/head", content: null);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        Assert.Equal("lock-me", await received.Content.ReadAsStringAsync());
        var lockToken = GetLockToken(received);

        var complete = await client.DeleteAsync($"{queue}/messages/{lockToken}");
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var peekAfter = await client.GetAsync($"{queue}/messages");
        using var doc = JsonDocument.Parse(await peekAfter.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ReceiveMessage_OnEmptyQueue_Returns204()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");

        var response = await client.PostAsync($"{queue}/messages/head", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveAndDelete_RemovesMessageImmediately()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("destructive-read"));

        var request = new HttpRequestMessage(HttpMethod.Delete, $"{queue}/messages/head");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("destructive-read", await response.Content.ReadAsStringAsync());

        var peekAfter = await client.GetAsync($"{queue}/messages");
        using var doc = JsonDocument.Parse(await peekAfter.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task DeadLetterMessage_RemovesItFromActiveQueue()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("bad-message"));
        var received = await client.PostAsync($"{queue}/messages/head", content: null);
        var lockToken = GetLockToken(received);

        var deadLetterBody = new StringContent("{\"deadLetterReason\":\"poison\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{queue}/messages/{lockToken}/deadletterqueue", deadLetterBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var peekAfter = await client.GetAsync($"{queue}/messages");
        using var doc = JsonDocument.Parse(await peekAfter.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task CompleteMessage_WithUnknownLockToken_Returns404()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");

        var response = await client.DeleteAsync($"{queue}/messages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveMessage_TwiceOnSingleMessageQueue_SecondCallSeesQueueAsEmpty()
    {
        // Once a message is peek-locked, it must be invisible to other receivers until it's
        // completed, dead-lettered, or the lock expires — otherwise two consumers could both
        // process the same message concurrently.
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("only-message"));

        var firstReceive = await client.PostAsync($"{queue}/messages/head", content: null);
        var secondReceive = await client.PostAsync($"{queue}/messages/head", content: null);

        Assert.Equal(HttpStatusCode.OK, firstReceive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondReceive.StatusCode);
    }

    [Fact]
    public async Task PeekMessages_MultipleMessages_ReturnsThemInFifoOrder()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("first"));
        await client.PostAsync($"{queue}/messages", new StringContent("second"));
        await client.PostAsync($"{queue}/messages", new StringContent("third"));

        var response = await client.GetAsync($"{queue}/messages");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var bodies = doc.RootElement.EnumerateArray().Select(m => m.GetProperty("body").GetString()).ToList();

        Assert.Equal(["first", "second", "third"], bodies);
    }

    [Fact]
    public async Task CompleteMessage_Twice_SecondCallReturns404()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("complete-once"));
        var received = await client.PostAsync($"{queue}/messages/head", content: null);
        var lockToken = GetLockToken(received);

        var first = await client.DeleteAsync($"{queue}/messages/{lockToken}");
        var second = await client.DeleteAsync($"{queue}/messages/{lockToken}");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task DeadLetterMessage_WithUnknownLockToken_Returns404()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");

        var response = await client.PostAsync($"{queue}/messages/{Guid.NewGuid()}/deadletterqueue", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Queues_AreIsolatedAcrossNamespaces()
    {
        var queueName = UniqueName("shared-queue-name");
        var clientA = NewClient(UniqueName("nsA"));
        var clientB = NewClient(UniqueName("nsB"));
        await clientA.PostAsync($"{queueName}/messages", new StringContent("only-in-a"));

        var peekB = await clientB.GetAsync($"{queueName}/messages");
        using var doc = JsonDocument.Parse(await peekB.Content.ReadAsStringAsync());

        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ReceiveMessage_IncrementsDeliveryCount()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        await client.PostAsync($"{queue}/messages", new StringContent("redelivered"));
        var received = await client.PostAsync($"{queue}/messages/head", content: null);
        var brokerProps = JsonDocument.Parse(received.Headers.GetValues("BrokerProperties").First());

        Assert.Equal(1, brokerProps.RootElement.GetProperty("DeliveryCount").GetInt32());
    }

    [Fact]
    public async Task SendMessage_WithApplicationPropertyHeader_IsCarriedOnPeek()
    {
        var client = NewClient(UniqueName("ns"));
        var queue = UniqueName("queue");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{queue}/messages")
        {
            Content = new StringContent("with-props")
        };
        request.Headers.Add("x-ms-prop-priority", "high");

        await client.SendAsync(request);

        var peek = await client.GetAsync($"{queue}/messages");
        using var doc = JsonDocument.Parse(await peek.Content.ReadAsStringAsync());
        var props = doc.RootElement.EnumerateArray().First().GetProperty("applicationProperties");
        Assert.Equal("high", props.GetProperty("priority").GetString());
    }
}
