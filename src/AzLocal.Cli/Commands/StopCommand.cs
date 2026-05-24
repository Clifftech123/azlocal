using System.CommandLine;
using System.Diagnostics;

namespace AzLocal.Cli.Commands;

public static class StopCommand
{
    public static Command Build()
    {
        var cmd = new Command("stop", "Stop the AzLocal host");

        cmd.SetAction((ParseResult _) =>
        {
            var pidFile = Path.Combine(Path.GetTempPath(), "azlocal", "host.pid");

            if (!File.Exists(pidFile))
            {
                Console.WriteLine("No running host found.");
                return;
            }

            var raw = File.ReadAllText(pidFile).Trim();

            if (!int.TryParse(raw, out var pid))
            {
                Console.Error.WriteLine($"PID file is corrupt ('{raw}'). Removing it.");
                File.Delete(pidFile);
                return;
            }

            try
            {
                var proc = Process.GetProcessById(pid);
                proc.CloseMainWindow(); // graceful first
                if (!proc.WaitForExit(3000))
                    proc.Kill();

                Console.WriteLine($"Stopped (pid {pid})");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Process already gone.");
            }
            finally
            {
                File.Delete(pidFile);
            }
        });

        return cmd;
    }
}
