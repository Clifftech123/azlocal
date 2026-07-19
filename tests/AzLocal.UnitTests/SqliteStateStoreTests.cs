using AzLocal.State;
using AzLocal.UnitTests.TestHelpers;
using Microsoft.Data.Sqlite;

namespace AzLocal.UnitTests;

public class SqliteStateStoreTests : IDisposable
{
    private record TestValue(string Name, int Count);

    private readonly string _dir;

    public SqliteStateStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "azlocal-tests", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools native connections, which keeps the db file
        // handle open on Windows even after `using var conn` disposes — clear the
        // pool first so the directory can actually be deleted.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SqliteStateStore NewStore() =>
        new(new InMemoryConfig("AzLocal:SqlitePath", _dir));

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        var store = NewStore();

        Assert.Null(await store.GetAsync<TestValue>("missing/key"));
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsValue()
    {
        var store = NewStore();
        var value = new TestValue("widget", 3);

        await store.SetAsync("blob/items/acct/container/widget", value);
        var result = await store.GetAsync<TestValue>("blob/items/acct/container/widget");

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingValue()
    {
        var store = NewStore();
        await store.SetAsync("key", new TestValue("first", 1));

        await store.SetAsync("key", new TestValue("second", 2));

        Assert.Equal(new TestValue("second", 2), await store.GetAsync<TestValue>("key"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingKey()
    {
        var store = NewStore();
        await store.SetAsync("key", new TestValue("gone", 0));


        await store.DeleteAsync("key");

        Assert.Null(await store.GetAsync<TestValue>("key"));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenKeyDoesNotExist()
    {
        var store = NewStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("missing/key"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var store = NewStore();

        Assert.False(await store.ExistsAsync("missing/key"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_AfterSet()
    {
        var store = NewStore();
        await store.SetAsync("key", new TestValue("present", 1));

        Assert.True(await store.ExistsAsync("key"));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyValuesMatchingPrefix()
    {
        var store = NewStore();
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
        var store = NewStore();
        var value = new TestValue("widget", 1);

        await store.SetAsync(setKey, value);

        Assert.Equal(value, await store.GetAsync<TestValue>(getKey));
    }

    [Fact]
    public async Task Data_PersistsAcrossStoreInstances_PointingAtSameDatabase()
    {
        var first = NewStore();
        await first.SetAsync("key", new TestValue("persisted", 42));

        var second = NewStore();

        Assert.Equal(new TestValue("persisted", 42), await second.GetAsync<TestValue>("key"));
    }
}
