using AzLocal.Core;
using System.CommandLine;

namespace AzLocal.Cli.Commands;

public static class WaitCommand
{
    public static Command Build()
    {
        var portOption = new Option<int>("--port")
        {
            Description         = "Port to poll",
            DefaultValueFactory = _ => EmulatorDefaults.DefaultPort,
        };

        var timeoutOption = new Option<int>("--timeout")
        {
            Description         = "Seconds to wait before giving up",
            DefaultValueFactory = _ => 30,
        };

        var cmd = new Command("wait", "Wait until AzLocal host is ready") { portOption, timeoutOption };

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var port        = pr.GetValue(portOption);
            var timeoutSecs = pr.GetValue(timeoutOption);
            var deadline    = DateTime.UtcNow.AddSeconds(timeoutSecs);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

            Console.Write($"Waiting for AzLocal on port {port}");

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = await http.GetAsync($"http://localhost:{port}/", ct);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(" ready.");
                        return;
                    }
                }
                catch { /* not up yet */ }

                Console.Write(".");
                await Task.Delay(1000, ct);
            }

            Console.WriteLine();
            Console.Error.WriteLine($"Timed out after {timeoutSecs}s — host did not respond on port {port}.");
        });

        return cmd;
    }
}
