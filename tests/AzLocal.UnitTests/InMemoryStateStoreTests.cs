using AzLocal.State;

namespace AzLocal.UnitTests;

public class InMemoryStateStoreTests
{
    private record TestValue(string Name, int Count);

    [Fact]

    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        var result = await store.GetAsync
            <TestValue>("missing/key");

        Assert.Null(result);
    }


    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsValue()
    {
        var store = new InMemoryStateStore();
        var value = new TestValue("widget", 3);

        await store.SetAsync("blob/items/acct/container/widget", value);
        var result = await store.GetAsync<TestValue>("blob/items/acct/container/widget");

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingValue()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("key", new TestValue("first", 1));

        await store.SetAsync("key", new TestValue("second", 2));
        var result = await store.GetAsync<TestValue>("key");

        Assert.Equal(new TestValue("second", 2), result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingKey()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("key", new TestValue("gone", 0));

        await store.DeleteAsync("key");

        Assert.Null(await store.GetAsync<TestValue>("key"));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("missing/key"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        Assert.False(await store.ExistsAsync("missing/key"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_AfterSet()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("key", new TestValue("present", 1));

        Assert.True(await store.ExistsAsync("key"));
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_WhenNoKeysMatchPrefix()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("blob/containers/acct/other", new TestValue("other", 1));

        var result = await store.ListAsync<TestValue>("blob/containers/acct/mine/");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyValuesMatchingPrefix()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("blob/containers/acct/c1", new TestValue("c1", 1));
        await store.SetAsync("blob/containers/acct/c2", new TestValue("c2", 2));
        await store.SetAsync("keyvault/secrets/acct/s1", new TestValue("s1", 3));

        var result = await store.ListAsync<TestValue>("blob/containers/acct/");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, v => v.Name == "c1");
        Assert.Contains(result, v => v.Name == "c2");
    }

    [Theory]
    [InlineData("Blob/Items/Acct/Widget", "blob/items/acct/widget")]
    [InlineData("blob/items/acct/widget", "BLOB/ITEMS/ACCT/WIDGET")]
    public async Task Keys_AreCaseInsensitive(string setKey, string getKey)
    {
        var store = new InMemoryStateStore();
        var value = new TestValue("widget", 1);

        await store.SetAsync(setKey, value);
        var result = await store.GetAsync<TestValue>(getKey);

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task ListAsync_PrefixMatch_IsCaseInsensitive()
    {
        var store = new InMemoryStateStore();
        await store.SetAsync("Blob/Containers/Acct/Mine", new TestValue("mine", 1));

        var result = await store.ListAsync<TestValue>("blob/containers/acct/");

        Assert.Single(result);
    }
}
