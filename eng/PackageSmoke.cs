using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using AzLocal.Client;

var endpoint = args[0];
var amqpPort = int.Parse(args[1]);
using var pinnedCertificate = File.Exists(args[2])
    ? System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(args[2])
    : null;
var thumbprint = pinnedCertificate?.Thumbprint ?? args[2];
using var transport = new HttpClientHandler
{
	ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.Thumbprint == thumbprint
};
var services = new ServiceCollection();
services.AddSingleton<TokenCredential>(provider => provider.GetRequiredService<Azure.Identity.ManagedIdentityCredential>());
services.AddAzLocal(options =>
{
	options.BaseUrl = endpoint;
	options.AmqpPort = amqpPort;
	options.BlobAccountName = "packagetest";
	options.BlobContainerName = "uploads";
	options.KeyVaultName = "packagevault";
	options.ServiceBusQueueName = "packagequeue";
	options.TransportHandler = transport;
});
await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var ct = timeout.Token;

var container = provider.GetRequiredService<BlobContainerClient>();
if (args.Length > 3 && args[3] == "verify")
{
    var persistedBlob = await container.GetBlobClient("hello.txt").DownloadContentAsync(ct);
    var persistedSecret = await provider.GetRequiredService<SecretClient>().GetSecretAsync("example", cancellationToken: ct);
    if (persistedBlob.Value.Content.ToString() != "packaged blob" || persistedSecret.Value.Value != "packaged secret")
        throw new Exception("Container data did not persist across restart.");
    Console.WriteLine("Container SQLite state and blob contents survived restart.");
    return;
}
await container.CreateIfNotExistsAsync(cancellationToken: ct);
var blob = container.GetBlobClient("hello.txt");
await blob.UploadAsync(new BinaryData("packaged blob"), cancellationToken: ct);
if ((await blob.DownloadContentAsync(ct)).Value.Content.ToString() != "packaged blob") throw new Exception("Blob round trip failed.");

var secrets = provider.GetRequiredService<SecretClient>();
await secrets.SetSecretAsync("example", "packaged secret", ct);
if ((await secrets.GetSecretAsync("example", cancellationToken: ct)).Value.Value != "packaged secret") throw new Exception("Secret round trip failed.");

await provider.GetRequiredService<ServiceBusSender>().SendMessageAsync(new ServiceBusMessage("packaged queue"), ct);
var receiver = provider.GetRequiredService<ServiceBusReceiver>();
var peek = await receiver.PeekMessageAsync(cancellationToken: ct);
if (peek?.Body.ToString() != "packaged queue") throw new Exception("Queue peek failed.");
var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3), ct);
if (received?.Body.ToString() != "packaged queue") throw new Exception("Queue receive failed.");
await receiver.CompleteMessageAsync(received, ct);

var arm = provider.GetRequiredService<ArmClient>();
var subscription = await arm.GetDefaultSubscriptionAsync(ct);
var created = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, "package-rg", new ResourceGroupData(new AzureLocation("eastus")), ct);
await created.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: ct);
Console.WriteLine("Azure SDK checks passed Blob, Key Vault, Service Bus, ARM, managed identity, and DI.");
