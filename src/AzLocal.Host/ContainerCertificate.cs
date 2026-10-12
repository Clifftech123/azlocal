using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AzLocal.Host;

internal static class ContainerCertificate
{
    internal static async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile("/certs/localhost.crt");
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, peer, _, _) => peer?.Thumbprint == certificate.Thumbprint
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.GetAsync("https://127.0.0.1:4566/");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    internal static void Create(string directory)
    {
        Directory.CreateDirectory(directory);
        var pfxPath = Path.Combine(directory, "localhost.pfx");
        if (File.Exists(pfxPath)) return;
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=azlocal localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var usages = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx, string.Empty));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(pfxPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.WriteAllText(Path.Combine(directory, "localhost.crt"), certificate.ExportCertificatePem());
        Console.WriteLine("Created a development HTTPS certificate in the container certificate volume.");
    }
}
