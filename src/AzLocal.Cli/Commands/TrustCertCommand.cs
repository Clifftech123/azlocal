using System.CommandLine;

namespace AzLocal.Cli.Commands;

public static class TrustCertCommand
{
    public static Command Build() => Build(CertificateHelper.TrustDevCert);

    internal static Command Build(Func<bool> trustCertificate)
    {
        var cmd = new Command("trust-cert", "Trust the AzLocal HTTPS developer certificate");

        cmd.SetAction((ParseResult _) =>
        {
            Console.WriteLine("Trusting developer certificate...");

            if (trustCertificate())
            {
                Console.WriteLine("Certificate trusted successfully.");
                return 0;
            }
            Console.Error.WriteLine("Failed to trust certificate. Run 'dotnet dev-certs https --trust --verbose' as the user who starts azlocal and follow the SDK instructions.");
            return 1;
        });

        return cmd;
    }
}
