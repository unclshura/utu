using Spectre.Console;
using Utu.Core;
using ValidationResult = Utu.Core.ValidationResult;

namespace Utu.Cli;

/// <summary>Renders plans, validation results and summaries.</summary>
public static class ReportWriter
{
    /// <summary>Writes the upload plan.</summary>
    public static void WritePlan(ConsoleUi ui, UploadPlan plan)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(new TableTitle("Upload plan"))
            .AddColumn(new TableColumn("Video"))
            .AddColumn(new TableColumn("Action"))
            .AddColumn(new TableColumn("Details"));

        foreach (var item in plan.Items)
        {
            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(item.Reason))
            {
                details.Add(item.Reason!);
            }

            details.AddRange(item.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Warning)
                .Select(issue => issue.Message));

            table.AddRow(
                new Text(item.File),
                new Markup(ActionLabel(item.Action)),
                new Text(string.Join("; ", details)));
        }

        ui.Write(table);
    }

    /// <summary>Writes the messages of a validation result.</summary>
    public static void WriteValidation(ConsoleUi ui, ValidationResult result)
    {
        foreach (var warning in result.Warnings)
        {
            ui.Warning($"{warning.Setting}: {warning.Message}");
        }

        foreach (var error in result.Errors)
        {
            ui.Error($"{error.Setting}: {error.Message}");
        }
    }

    /// <summary>Writes the result of a run.</summary>
    public static void WriteSummary(ConsoleUi ui, UploadSummary summary)
    {
        if (summary.Outcomes.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(new TableTitle("Result"))
            .AddColumn(new TableColumn("Video"))
            .AddColumn(new TableColumn("Result"))
            .AddColumn(new TableColumn("Details"));

        foreach (var outcome in summary.Outcomes)
        {
            var details = new List<string>();

            if (outcome.VideoUrl is { Length: > 0 } url)
            {
                details.Add(url);
            }

            if (outcome.Error is { Length: > 0 } error)
            {
                details.Add(error);
            }

            details.AddRange(outcome.Notes.Where(note => !note.StartsWith("manual:", StringComparison.Ordinal)));

            table.AddRow(
                new Text(outcome.Item.File),
                new Markup(StatusLabel(outcome.Status)),
                new Text(string.Join("; ", details)));
        }

        ui.Write(table);
    }

    /// <summary>Writes the settings that have to be applied in YouTube Studio.</summary>
    public static void WriteManualFollowUps(ConsoleUi ui, UploadSummary summary)
    {
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var outcome in summary.Outcomes.Where(outcome => outcome.Succeeded))
        {
            foreach (var note in outcome.Notes.Where(note => note.StartsWith("manual:", StringComparison.Ordinal)))
            {
                var text = note["manual:".Length..].Trim();

                if (shown.Add(text))
                {
                    ui.Manual($"{outcome.Item.File}: {text}");
                }
            }
        }
    }

    private static string ActionLabel(UploadAction action) => action switch
    {
        UploadAction.Upload => "[deepskyblue1]upload[/]",
        UploadAction.Resume => "[deepskyblue1]continue[/]",
        UploadAction.ApplyExtras => "[deepskyblue1]extras[/]",
        UploadAction.Skip => "[grey]skip[/]",
        _ => "[red]blocked[/]",
    };

    private static string StatusLabel(UploadOutcomeStatus status) => status switch
    {
        UploadOutcomeStatus.Uploaded => "[green]uploaded[/]",
        UploadOutcomeStatus.Resumed => "[green]continued[/]",
        UploadOutcomeStatus.Updated => "[green]updated[/]",
        _ => "[red]failed[/]",
    };
}