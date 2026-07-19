using AzLocal.State;
using AzLocal.UnitTests.TestHelpers;
using System.Text;

namespace AzLocal.UnitTests;

public class TempFileBlobStoreTests : IDisposable
{
    private readonly string _dir;

    public TempFileBlobStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "azlocal-tests", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private TempFileBlobStore NewStore() =>
        new(new InMemoryConfig("AzLocal:BlobStorePath", _dir));

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsContent()
    {
        var store = NewStore();

        await store.WriteAsync("mycontainer", "file.txt", ToStream("hello world"), "text/plain");

        await using var stream = await store.ReadAsync("mycontainer", "file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task WriteAsync_OverwritesExistingBlob()
    {
        var store = NewStore();
        await store.WriteAsync("mycontainer", "file.txt", ToStream("first"), "text/plain");

        await store.WriteAsync("mycontainer", "file.txt", ToStream("second"), "text/plain");

        await using var stream = await store.ReadAsync("mycontainer", "file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("second", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task WriteAsync_CreatesIntermediateDirectories_ForNestedBlobNames()
    {
        var store = NewStore();

        await store.WriteAsync("mycontainer", "folder/subfolder/file.txt", ToStream("nested"), "text/plain");

        await using var stream = await store.ReadAsync("mycontainer", "folder/subfolder/file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("nested", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.ReadAsync("mycontainer", "missing.txt"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        Assert.False(await store.ExistsAsync("mycontainer", "missing.txt"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_AfterWrite()
    {
        var store = NewStore();
        await store.WriteAsync("mycontainer", "file.txt", ToStream("data"), "text/plain");

        Assert.True(await store.ExistsAsync("mycontainer", "file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesBlob()
    {
        var store = NewStore();
        await store.WriteAsync("mycontainer", "file.txt", ToStream("data"), "text/plain");

        await store.DeleteAsync("mycontainer", "file.txt");

        Assert.False(await store.ExistsAsync("mycontainer", "file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("mycontainer", "missing.txt"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteContainerAsync_RemovesAllBlobsInContainer()
    {
        var store = NewStore();
        await store.WriteAsync("mycontainer", "a.txt", ToStream("a"), "text/plain");
        await store.WriteAsync("mycontainer", "folder/b.txt", ToStream("b"), "text/plain");

        await store.DeleteContainerAsync("mycontainer");

        Assert.False(await store.ExistsAsync("mycontainer", "a.txt"));
        Assert.False(await store.ExistsAsync("mycontainer", "folder/b.txt"));
    }

    [Fact]
    public async Task DeleteContainerAsync_DoesNotAffectOtherContainers()
    {
        var store = NewStore();
        await store.WriteAsync("keep", "a.txt", ToStream("a"), "text/plain");
        await store.WriteAsync("remove", "b.txt", ToStream("b"), "text/plain");

        await store.DeleteContainerAsync("remove");

        Assert.True(await store.ExistsAsync("keep", "a.txt"));
    }

    [Fact]
    public async Task DeleteContainerAsync_DoesNotThrow_WhenContainerDoesNotExist()
    {
        var store = NewStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteContainerAsync("missing-container"));

        Assert.Null(exception);
    }
}
