
using AzLocal.IntegrationTests.Fixtures;
using Azure;
using System.Text;

namespace AzLocal.IntegrationTests;

public class BlobStorageTests : IClassFixture<EmulatorFixture>
{
    private readonly EmulatorFixture _fixture;

    public BlobStorageTests(EmulatorFixture fixture) => _fixture = fixture;

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateContainer_ThenUploadAndDownloadBlob_RoundTripsContent()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient("file.txt");
        await blobClient.UploadAsync(new BinaryData("hello from azlocal"), overwrite: true);

        var download = await blobClient.DownloadContentAsync();

        Assert.Equal("hello from azlocal", download.Value.Content.ToString());
    }

    [Fact]
    public async Task GetBlobProperties_ReflectsUploadedContentType()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("data.json");

        await blobClient.UploadAsync(
            new BinaryData(Encoding.UTF8.GetBytes("{}")),
            new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = "application/json" }
            });

        var props = await blobClient.GetPropertiesAsync();

        Assert.Equal("application/json", props.Value.ContentType);
    }

    [Fact]
    public async Task ListBlobs_ReturnsUploadedBlob()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        await containerClient.UploadBlobAsync("only-blob.txt", new BinaryData("content"));

        var names = new List<string>();
        await foreach (var item in containerClient.GetBlobsAsync())
            names.Add(item.Name);

        Assert.Contains("only-blob.txt", names);
    }

    [Fact]
    public async Task DeleteBlob_RemovesIt()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("to-delete.txt");
        await blobClient.UploadAsync(new BinaryData("gone soon"), overwrite: true);

        await blobClient.DeleteAsync();

        await Assert.ThrowsAsync<RequestFailedException>(() => blobClient.DownloadContentAsync());
    }

    [Fact]
    public async Task DownloadBlob_ThatWasNeverUploaded_Throws404()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("never-existed.txt");

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => blobClient.DownloadContentAsync());
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task CreateContainer_Twice_WithPlainCreate_ThrowsConflict()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateAsync();

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => containerClient.CreateAsync());
        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task CreateContainer_Twice_WithCreateIfNotExists_DoesNotThrow()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();

        var exception = await Record.ExceptionAsync(() => containerClient.CreateIfNotExistsAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UploadBlob_WithNestedPath_RoundTripsContent()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("folder/subfolder/file.txt");

        await blobClient.UploadAsync(new BinaryData("nested content"), overwrite: true);
        var download = await blobClient.DownloadContentAsync();

        Assert.Equal("nested content", download.Value.Content.ToString());
    }

    [Fact]
    public async Task UploadBlob_Overwrite_ReplacesContent()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("overwrite-me.txt");
        await blobClient.UploadAsync(new BinaryData("original"), overwrite: true);

        await blobClient.UploadAsync(new BinaryData("replaced"), overwrite: true);
        var download = await blobClient.DownloadContentAsync();

        Assert.Equal("replaced", download.Value.Content.ToString());
    }

    [Fact]
    public async Task UploadBlob_BinaryContent_RoundTripsExactBytes()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("binary.dat");
        var bytes = new byte[] { 0x00, 0x01, 0xFF, 0x7F, 0x80, 0xDE, 0xAD, 0xBE, 0xEF };

        await blobClient.UploadAsync(new BinaryData(bytes), overwrite: true);
        var download = await blobClient.DownloadContentAsync();

        Assert.Equal(bytes, download.Value.Content.ToArray());
    }

    [Fact]
    public async Task GetProperties_ForMissingBlob_Throws404()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("never-existed.txt");

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => blobClient.GetPropertiesAsync());
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task BlobExists_ReturnsFalseThenTrueAfterUpload()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("exists-check.txt");

        var before = await blobClient.ExistsAsync();
        await blobClient.UploadAsync(new BinaryData("now it exists"), overwrite: true);
        var after = await blobClient.ExistsAsync();

        Assert.False(before.Value);
        Assert.True(after.Value);
    }

    [Fact]
    public async Task DeleteContainer_RemovesAllBlobs()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient("doomed.txt");
        await blobClient.UploadAsync(new BinaryData("about to vanish"), overwrite: true);

        await containerClient.DeleteAsync();

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => blobClient.DownloadContentAsync());
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task Containers_AreIsolatedAcrossAccounts()
    {
        var containerName = UniqueName("shared-name-container");
        var accountA = _fixture.Clients.CreateBlobContainerClient(UniqueName("acctA"), containerName);
        var accountB = _fixture.Clients.CreateBlobContainerClient(UniqueName("acctB"), containerName);
        await accountA.CreateIfNotExistsAsync();
        await accountA.UploadBlobAsync("only-in-a.txt", new BinaryData("a's data"));

        var namesInB = new List<string>();
        await foreach (var item in accountB.GetBlobsAsync())
            namesInB.Add(item.Name);

        Assert.DoesNotContain("only-in-a.txt", namesInB);
    }

    [Fact]
    public async Task ListBlobs_OnEmptyContainer_ReturnsNoBlobs()
    {
        var containerClient = _fixture.Clients.CreateBlobContainerClient("acct1", UniqueName("container"));
        await containerClient.CreateIfNotExistsAsync();

        var names = new List<string>();
        await foreach (var item in containerClient.GetBlobsAsync())
            names.Add(item.Name);

        Assert.Empty(names);
    }
}
