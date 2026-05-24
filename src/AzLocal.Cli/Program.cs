using AzLocal.Cli.Commands;
using System.CommandLine;

var rootCommand = new RootCommand("AzLocal — local Azure emulator CLI");

rootCommand.Add(StartCommand.Build());
rootCommand.Add(StopCommand.Build());
rootCommand.Add(StatusCommand.Build());
rootCommand.Add(WaitCommand.Build());
rootCommand.Add(ResetCommand.Build());
rootCommand.Add(TrustCertCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
