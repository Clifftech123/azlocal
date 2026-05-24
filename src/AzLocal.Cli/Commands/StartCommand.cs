using AzLocal.Core;
using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AzLocal.Cli.Commands;

public static class StartCommand
{
    public static Command Build()
    {
        var portOption = new Option<int>("--port")
        {
            Description = "Port to listen on",
            DefaultValueFactory = _ => EmulatorDefaults.DefaultPort,
        };

        var cmd = new Command("start", "Start the AzLocal host") { portOption };

        cmd.SetAction((ParseResult pr) =>
        {
            var port = pr.GetValue(portOption);

            if (port is < 1 or > 65535)
            {
                Console.Error.WriteLine($"Invalid port: {port}. Must be 1-65535.");
                return;
            }

            var pidDir = Path.Combine(Path.GetTempPath(), "azlocal");
            var pidFile = Path.Combine(pidDir, "host.pid");

            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var existing))
            {
                try
                {
                    Process.GetProcessById(existing);
                    Console.Error.WriteLine($"AzLocal is already running (pid {existing}). Run 'stop' first.");
                    return;
                }
                catch (ArgumentException) { /* stale pid file */ }
            }

            Directory.CreateDirectory(pidDir);

            var proc = Process.Start(BuildHostProcessInfo(port));

            if (proc is null)
            {
                Console.Error.WriteLine("Failed to start host process.");
                return;
            }

            File.WriteAllText(pidFile, proc.Id.ToString());
            Console.WriteLine($"AzLocal started on http://localhost:{port} (pid {proc.Id})");
        });

        return cmd;
    }

    private static ProcessStartInfo BuildHostProcessInfo(int port)
    {
        // When published, AzLocal.Host lives next to this binary.
        // Fall back to 'dotnet run' for local development.
        var hostExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(AppContext.BaseDirectory, "AzLocal.Host.exe")
            : Path.Combine(AppContext.BaseDirectory, "AzLocal.Host");

        if (File.Exists(hostExe))
        {
            return new ProcessStartInfo
            {
                FileName = hostExe,
                Arguments = $"--urls http://localhost:{port}",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project src/AzLocal.Host --urls http://localhost:{port}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
