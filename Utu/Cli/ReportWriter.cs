using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;
using Utu.Configuration;
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

    /// <summary>Writes the publication times that a publish schedule produced.</summary>
    /// <param name="ui">Console to write to.</param>
    /// <param name="expression">The crontab expression.</param>
    /// <param name="videos">The videos, in the order they were scheduled.</param>
    public static void WriteSchedule(ConsoleUi ui, string expression, IReadOnlyList<VideoEntry> videos)
    {
        var scheduled = videos.Where(entry => entry.PublishAt is not null).ToList();

        if (scheduled.Count == 0)
        {
            return;
        }

        var first = scheduled[0].PublishAt!.Value;
        var last = scheduled[^1].PublishAt!.Value;

        ui.Info(scheduled.Count == 1
            ? $"publish schedule '{expression}': 1 video at {Moment(first)}"
            : $"publish schedule '{expression}': {scheduled.Count} videos, {Moment(first)} to {Moment(last)}");
    }

    /// <summary>Warns about scheduled publications that YouTube will not honour.</summary>
    /// <param name="ui">Console to write to.</param>
    /// <param name="config">The merged configuration the videos belong to.</param>
    /// <param name="videos">The videos that carry a publication time.</param>
    public static void WriteScheduleWarnings(
        ConsoleUi ui,
        ResolvedConfiguration config,
        IReadOnlyList<VideoEntry> videos)
    {
        var scheduled = videos.Where(entry => entry.PublishAt is not null).ToList();

        if (scheduled.Count == 0)
        {
            return;
        }

        // YouTube only accepts a publication time on a private video; a public or
        // unlisted video with a publication time is rejected at upload.
        var publicVideos = scheduled
            .Count(entry => SettingsResolver.Resolve(entry, config.Defaults).Privacy is not Privacy.Private);

        if (publicVideos > 0)
        {
            ui.Warning(
                $"{publicVideos} of the {scheduled.Count} scheduled videos does not resolve to 'privacy: private'; " +
                "YouTube only publishes a private video at a set time");
        }

        if (scheduled.Min(entry => entry.PublishAt!.Value) - DateTimeOffset.Now < VideoSettingsValidator.MinimumSchedulingLeadTime)
        {
            ui.Warning(
                $"the first publication is less than {VideoSettingsValidator.MinimumSchedulingLeadTime.TotalMinutes:0} " +
                "minutes away; YouTube may publish the video right away");
        }
    }

    /// <summary>Writes the publication time of every video.</summary>
    /// <param name="ui">Console to write to.</param>
    /// <param name="videos">The videos, in the order they were scheduled.</param>
    /// <param name="previous">
    /// The publication times the videos had before; a "Was" column is added when
    /// at least one of them changed.
    /// </param>
    public static void WritePublicationTimes(
        ConsoleUi ui,
        IReadOnlyList<VideoEntry> videos,
        IReadOnlyList<DateTimeOffset?>? previous = null)
    {
        var was = previous is not null &&
            previous.Count == videos.Count &&
            videos.Where((entry, index) => entry.PublishAt != previous[index]).Any();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(new TableTitle("Publication times"))
            .AddColumn(new TableColumn("Video"));

        if (was)
        {
            table.AddColumn(new TableColumn("Was"));
        }

        table.AddColumn(new TableColumn("Publish at"));

        for (var index = 0; index < videos.Count; index++)
        {
            var row = new List<IRenderable> { new Text(videos[index].File ?? "(no file)") };

            if (was)
            {
                row.Add(new Text(previous![index] is { } before ? Moment(before) : "-"));
            }

            row.Add(new Text(videos[index].PublishAt is { } moment ? Moment(moment) : "-"));
            table.AddRow(row);
        }

        ui.Write(table);
    }

    /// <summary>Formats a moment with the offset it was computed in.</summary>
    private static string Moment(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);

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