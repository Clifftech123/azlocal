using AzLocal.Cli.Commands;
using System.CommandLine;



var rootCommand = new RootCommand("AzLocal — local Azure emulator CLI");
rootCommand.Options.Add(StartCommand.Build());
rootCommand.Options.Add(StopCommand);
rootCommand.Options.Add(StatusCommand);
rootCommand.Options.Add(ResetCommand);
rootCommand.Options.Add(TrustCertCommand);
rootCommand.Options.Add(WaitCommand);


