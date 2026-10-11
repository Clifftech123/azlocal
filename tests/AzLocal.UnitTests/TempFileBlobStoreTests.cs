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

    [Theory]
    [InlineData("../../outside.txt")]
    [InlineData("C:\\outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("..\\..\\outside.txt")]
    public async Task UntrustedNamesRemainInsideStorage(string name)
    {
        var store = NewStore();
        await store.WriteAsync("../../account", "../container", name, ToStream("safe"), "text/plain");
        var files = Directory.GetFiles(_dir, "*", SearchOption.AllDirectories);
        Assert.Single(files);
        Assert.StartsWith(Path.GetFullPath(_dir) + Path.DirectorySeparatorChar, files[0]);
        await using var stream = await store.ReadAsync("../../account", "../container", name);
        Assert.Equal(4, stream.Length);
    }

    [Fact]
    public async Task AccountsAndCaseSensitiveBlobNamesAreIsolated()
    {
        var store = NewStore();
        await store.WriteAsync("a", "shared", "File", ToStream("first"), "text/plain");
        await store.WriteAsync("b", "shared", "File", ToStream("second"), "text/plain");
        await store.WriteAsync("b", "shared", "file", ToStream("third"), "text/plain");
        await store.DeleteContainerAsync("a", "shared");
        await using var upper = await store.ReadAsync("b", "shared", "File");
        await using var lower = await store.ReadAsync("b", "shared", "file");
        Assert.Equal("second", await new StreamReader(upper).ReadToEndAsync());
        Assert.Equal("third", await new StreamReader(lower).ReadToEndAsync());
    }

    private TempFileBlobStore NewStore() =>
        new(new InMemoryConfig("AzLocal:BlobStorePath", _dir));

    [Fact]
    public async Task SystemAncestorLinksDoNotPreventBlobStorage() {
        // Windows link creation requires privileges; Unix temporary directories
        // commonly have linked ancestors, which this case reproduces.
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_dir);
        var physical = Path.Combine(_dir, "physical");
        Directory.CreateDirectory(physical);
        var linked = Path.Combine(_dir, "linked");
        Directory.CreateSymbolicLink(linked, physical);
        var store = new TempFileBlobStore(new InMemoryConfig("AzLocal:BlobStorePath", Path.Combine(linked, "blobs")));
        await store.WriteAsync("account", "container", "file", ToStream("linked ancestor"), "text/plain");
        await using var content = await store.ReadAsync("account", "container", "file");
        Assert.Equal("linked ancestor", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public async Task LinksInsideTheBlobRootRemainRejected() {
        if (OperatingSystem.IsWindows()) return;
        var store = NewStore();
        await store.WriteAsync("account", "container", "file", ToStream("original"), "text/plain");
        var accountDirectory = Assert.Single(Directory.GetDirectories(_dir));
        var containerDirectory = Assert.Single(Directory.GetDirectories(accountDirectory));
        var filePath = Assert.Single(Directory.GetFiles(containerDirectory));
        File.Delete(filePath);
        var outsidePath = Path.Combine(_dir, "outside");
        await File.WriteAllTextAsync(outsidePath, "unchanged");
        File.CreateSymbolicLink(filePath, outsidePath);
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("account", "container", "file", ToStream("blocked"), "text/plain"));
        Assert.Equal("unchanged", await File.ReadAllTextAsync(outsidePath));
    }

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsContent()
    {
        var store = NewStore();

        await store.WriteAsync("acct", "mycontainer", "file.txt", ToStream("hello world"), "text/plain");

        await using var stream = await store.ReadAsync("acct", "mycontainer", "file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task WriteAsync_OverwritesExistingBlob()
    {
        var store = NewStore();
        await store.WriteAsync("acct", "mycontainer", "file.txt", ToStream("first"), "text/plain");

        await store.WriteAsync("acct", "mycontainer", "file.txt", ToStream("second"), "text/plain");

        await using var stream = await store.ReadAsync("acct", "mycontainer", "file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("second", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task WriteAsync_CreatesIntermediateDirectories_ForNestedBlobNames()
    {
        var store = NewStore();

        await store.WriteAsync("acct", "mycontainer", "folder/subfolder/file.txt", ToStream("nested"), "text/plain");

        await using var stream = await store.ReadAsync("acct", "mycontainer", "folder/subfolder/file.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("nested", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.ReadAsync("acct", "mycontainer", "missing.txt"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        Assert.False(await store.ExistsAsync("acct", "mycontainer", "missing.txt"));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_AfterWrite()
    {
        var store = NewStore();
        await store.WriteAsync("acct", "mycontainer", "file.txt", ToStream("data"), "text/plain");

        Assert.True(await store.ExistsAsync("acct", "mycontainer", "file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesBlob()
    {
        var store = NewStore();
        await store.WriteAsync("acct", "mycontainer", "file.txt", ToStream("data"), "text/plain");

        await store.DeleteAsync("acct", "mycontainer", "file.txt");

        Assert.False(await store.ExistsAsync("acct", "mycontainer", "file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenBlobDoesNotExist()
    {
        var store = NewStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("acct", "mycontainer", "missing.txt"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteContainerAsync_RemovesAllBlobsInContainer()
    {
        var store = NewStore();
        await store.WriteAsync("acct", "mycontainer", "a.txt", ToStream("a"), "text/plain");
        await store.WriteAsync("acct", "mycontainer", "folder/b.txt", ToStream("b"), "text/plain");

        await store.DeleteContainerAsync("acct", "mycontainer");

        Assert.False(await store.ExistsAsync("acct", "mycontainer", "a.txt"));
        Assert.False(await store.ExistsAsync("acct", "mycontainer", "folder/b.txt"));
    }

    [Fact]
    public async Task DeleteContainerAsync_DoesNotAffectOtherContainers()
    {
        var store = NewStore();
        await store.WriteAsync("acct", "keep", "a.txt", ToStream("a"), "text/plain");
        await store.WriteAsync("acct", "remove", "b.txt", ToStream("b"), "text/plain");

        await store.DeleteContainerAsync("acct", "remove");

        Assert.True(await store.ExistsAsync("acct", "keep", "a.txt"));
    }

    [Fact]
    public async Task DeleteContainerAsync_DoesNotThrow_WhenContainerDoesNotExist()
    {
        var store = NewStore();

        var exception = await Record.ExceptionAsync(() => store.DeleteContainerAsync("acct", "missing-container"));

        Assert.Null(exception);
    }
}
