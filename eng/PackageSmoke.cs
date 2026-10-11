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
var thumbprint = args[2];
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
Console.WriteLine("Installed NuGet packages passed Blob, Key Vault, Service Bus, ARM, managed identity, and DI checks.");
