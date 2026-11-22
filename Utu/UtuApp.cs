using Utu.Cli;
using Utu.Commands;

namespace Utu;

/// <summary>Wires the command line to the commands.</summary>
public static class UtuApp
{
    /// <summary>Runs utu and returns the process exit code.</summary>
    public static async Task<int> RunAsync(CommandLine line, CancellationToken cancellationToken = default)
    {
        var ui = new ConsoleUi(line.NoColor, line.Verbose);

        switch (line.Command)
        {
            case UtuCommand.Init:
                return InitCommand.Run(line, ui);

            case UtuCommand.Schedule:
                return ScheduleCommand.Run(line, ui);

            case UtuCommand.Upload:
                return await UploadCommand.RunAsync(line, ui, cancellationToken).ConfigureAwait(false);

            default:
                WriteHelp(ui);
                return 0;
        }
    }

    /// <summary>Writes the help text.</summary>
    public static void WriteHelp(ConsoleUi ui)
    {
        ui.Markup("[bold deepskyblue1]utu[/] - " + Spectre.Console.Markup.Escape(HelpText.Summary));
        ui.Blank();

        foreach (var line in HelpText.Lines)
        {
            ui.Plain(line);
        }
    }
}