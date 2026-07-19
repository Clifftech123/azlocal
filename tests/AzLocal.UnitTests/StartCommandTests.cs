using AzLocal.Cli.Commands;

namespace AzLocal.UnitTests;

public class StartCommandTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4566)]
    [InlineData(65535)]
    public void IsValidPort_AcceptsInRangeValues(int port)
    {
        Assert.True(StartCommand.IsValidPort(port));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void IsValidPort_RejectsOutOfRangeValues(int port)
    {
        Assert.False(StartCommand.IsValidPort(port));
    }

    [Fact]
    public void BuildHostProcessInfo_WithNoPublishedHostExe_FallsBackToDotnetRun()
    {
        // The test binary's own output directory never contains AzLocal.Host.exe (AzLocal.UnitTests
        // has no dependency on AzLocal.Host), so this deterministically exercises the dev fallback.
        var psi = StartCommand.BuildHostProcessInfo(4566);

        Assert.Equal("dotnet", psi.FileName);
        Assert.Equal("run --project src/AzLocal.Host --urls https://127.0.0.1:4566", psi.Arguments);
    }

    [Fact]
    public void BuildHostProcessInfo_DoesNotUseShellExecute_AndSuppressesWindow()
    {
        var psi = StartCommand.BuildHostProcessInfo(4566);

        Assert.False(psi.UseShellExecute);
        Assert.True(psi.CreateNoWindow);
    }

    [Fact]
    public void BuildHostProcessInfo_EmbedsRequestedPort_InArguments()
    {
        var psi = StartCommand.BuildHostProcessInfo(9999);

        Assert.Contains("--urls https://127.0.0.1:9999", psi.Arguments);
    }
}
