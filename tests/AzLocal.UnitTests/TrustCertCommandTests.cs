using AzLocal.Cli.Commands;
using System.CommandLine;

namespace AzLocal.UnitTests;

public class TrustCertCommandTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void ExitCodeReflectsCertificateSetupResult(bool succeeded, int expectedCode)
    {
        // No certificate store changes: exercise the CLI result used by scripts.
        var root = new RootCommand { TrustCertCommand.Build(() => succeeded) };
        Assert.Equal(expectedCode, root.Parse("trust-cert").Invoke());
    }
}
