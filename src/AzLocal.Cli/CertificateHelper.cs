using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AzLocal.Cli;

public static class CertificateHelper
{
    public static bool TrustDevCert()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Run("dotnet", "dev-certs https -ep /usr/local/share/ca-certificates/azlocal-dev.crt --format PEM --no-password");
                Run("sudo", "update-ca-certificates");
            }
            else
            {
                // Works on Windows and macOS
                Run("dotnet", "dev-certs https --trust");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Certificate error: {ex.Message}");
            return false;
        }
    }

    private static void Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false };
        using var proc = Process.Start(psi) ?? throw new Exception($"Could not start '{exe}'.");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new Exception($"'{exe}' exited with code {proc.ExitCode}.");
    }
}
