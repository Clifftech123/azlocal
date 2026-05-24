using System.CommandLine;
using System.Diagnostics;

namespace AzLocal.Cli.Commands;

public static class ResetCommand
{
    public static Command Build()
    {
        var cmd = new Command("reset", "Stop the AzLocal host and wipe all local state");

        cmd.SetAction((ParseResult _) =>
        {
            var azlocalDir = Path.Combine(Path.GetTempPath(), "azlocal");
            var pidFile    = Path.Combine(azlocalDir, "host.pid");

            // Stop any running host
            if (File.Exists(pidFile))
            {
                var raw = File.ReadAllText(pidFile).Trim();
                if (int.TryParse(raw, out var pid))
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        proc.CloseMainWindow();
                        if (!proc.WaitForExit(3000))
                            proc.Kill();
                        Console.WriteLine($"Stopped running host (pid {pid}).");
                    }
                    catch (ArgumentException) { /* already gone */ }
                }
            }

            // Wipe all state under the azlocal temp directory
            if (Directory.Exists(azlocalDir))
            {
                Directory.Delete(azlocalDir, recursive: true);
                Console.WriteLine("State cleared.");
            }
            else
            {
                Console.WriteLine("Nothing to reset.");
            }
        });

        return cmd;
    }
}
