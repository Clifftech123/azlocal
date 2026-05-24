using AzLocal.Core;
using System.CommandLine;

namespace AzLocal.Cli.Commands;

public static class StatusCommand
{
    public static Command Build()
    {
        var portOption = new Option<int>("--port")
        {
            Description         = "Port to check",
            DefaultValueFactory = _ => EmulatorDefaults.DefaultPort,
        };

        var cmd = new Command("status", "Check if AzLocal is running") { portOption };

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var port = pr.GetValue(portOption);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            try
            {
                var response = await http.GetAsync($"http://localhost:{port}/", ct);
                Console.WriteLine($"RUNNING on port {port} (HTTP {(int)response.StatusCode})");
            }
            catch
            {
                Console.WriteLine($"NOT running on port {port}");
            }
        });

        return cmd;
    }
}
