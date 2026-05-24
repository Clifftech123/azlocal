using System.CommandLine;

namespace AzLocal.Cli.Commands;

public static class TrustCertCommand
{
    public static Command Build()
    {
        var cmd = new Command("trust-cert", "Trust the AzLocal HTTPS developer certificate");

        cmd.SetAction((ParseResult _) =>
        {
            Console.WriteLine("Trusting developer certificate...");

            if (CertificateHelper.TrustDevCert())
                Console.WriteLine("Certificate trusted successfully.");
            else
                Console.Error.WriteLine("Failed to trust certificate. Try running as administrator/sudo.");
        });

        return cmd;
    }
}
