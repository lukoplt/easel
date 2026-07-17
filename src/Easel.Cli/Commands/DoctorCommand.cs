using Easel.Cli.Infrastructure;
using Easel.Fx;
using Easel.Pac;
using Spectre.Console;

namespace Easel.Cli.Commands;

/// <summary>`easel doctor` — environment diagnostics.</summary>
public sealed class DoctorCommand
{
    public int Run()
    {
        var table = new Table().RoundedBorder();
        table.AddColumn("Check");
        table.AddColumn("Status");
        table.AddColumn("Detail");

        bool ok = true;

        table.AddRow("easel", "[green]ok[/]", $"{ToolInfo.ToolName} {ToolInfo.Version}");

        // pac
        var pac = PacRunner.Detect();
        if (!pac.Found)
        {
            ok = false;
            table.AddRow("pac CLI", "[red]missing[/]", "install: dotnet tool install --global Microsoft.PowerApps.CLI.Tool");
        }
        else if (!pac.VersionSupported)
        {
            ok = false;
            table.AddRow("pac CLI", "[yellow]outdated[/]", $"{pac.Version} < min {PacRunner.MinSupportedVersion}");
        }
        else
        {
            table.AddRow("pac CLI", "[green]ok[/]", $"{pac.Version} ({pac.Path})");
        }

        // Power Fx parser
        try
        {
            var parse = new FxParseService().Parse("Set(x, 1 + 2)");
            var status = parse.IsSuccess ? "[green]ok[/]" : "[red]failed[/]";
            if (!parse.IsSuccess) ok = false;
            table.AddRow("Power Fx parser", status, "Microsoft.PowerFx.Core");
        }
        catch (Exception ex)
        {
            ok = false;
            table.AddRow("Power Fx parser", "[red]error[/]", ex.Message);
        }

        table.AddRow("pa.yaml schema", "[green]ok[/]", $"supported: v{ToolInfo.SupportedPaYamlSchema}");

        // temp writable
        try
        {
            var probe = Path.Combine(Path.GetTempPath(), "easel", "doctor-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(probe);
            File.WriteAllText(Path.Combine(probe, "probe.txt"), "ok");
            Directory.Delete(probe, recursive: true);
            table.AddRow("temp writable", "[green]ok[/]", Path.GetTempPath());
        }
        catch (Exception ex)
        {
            ok = false;
            table.AddRow("temp writable", "[red]failed[/]", ex.Message);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(ok ? "[green]Environment ready.[/]" : "[yellow]Some checks need attention.[/]");
        return ok ? ExitCode.Ok : ExitCode.PacMissingOrIncompatible;
    }
}
