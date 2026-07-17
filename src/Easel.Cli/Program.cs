using System.CommandLine;
using Easel.Cli.Commands;

var root = new RootCommand("easel — static analysis for Power Apps canvas source");

var doctor = new Command("doctor", "Check the environment (pac, Power Fx, temp).");
doctor.SetAction(_ => new DoctorCommand().Run());
root.Subcommands.Add(doctor);

root.Subcommands.Add(LintCommand.Build());
root.Subcommands.Add(StatsCommand.Build());
root.Subcommands.Add(AnalyzeCommand.Build());
root.Subcommands.Add(SecretsCommand.Build());
root.Subcommands.Add(DiffCommand.Build());
root.Subcommands.Add(RenameCommand.Build());
root.Subcommands.Add(ExplainCommand.Build());
root.Subcommands.Add(FixCommand.Build());

return await root.Parse(args).InvokeAsync();
