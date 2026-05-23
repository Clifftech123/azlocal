using System.CommandLine;
using System.Diagnostics;

namespace AzLocal.Cli.Commands;

public static class StartCommand
{
    public static Command Build()
    {
        var portOption = new Option<int>("--port");

        var cmd = new Command("start", "Start the AzLocal host") { portOption };

        cmd.SetAction((ParseResult pr) =>
        {
            var port = pr.GetRequiredValue(portOption);
            // Option<int> in scope
            var pidDir = Path.Combine(Path.GetTempPath(), "azlocal");
            Directory.CreateDirectory(pidDir);

            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project src/AzLocal.Host --urls http://localhost:{port}",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (proc is null) { Console.Error.WriteLine("Failed to start."); return; }

            File.WriteAllText(Path.Combine(pidDir, "host.pid"), proc.Id.ToString());
            Console.WriteLine($"AzLocal started — http://localhost:{port} (pid {proc.Id})");
        });
        return cmd;
    }
}