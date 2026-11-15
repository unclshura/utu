using Spectre.Console;
using Utu.Core;

namespace Utu.Cli;

/// <summary>
/// Renders the progress of an upload run. With a <see cref="ProgressContext" />
/// every video gets a progress bar; without one a line is printed per event.
/// </summary>
public sealed class ConsoleUploadObserver : IUploadObserver
{
    private readonly ConsoleUi _ui;
    private readonly ProgressContext? _context;
    private readonly Dictionary<string, ProgressTask> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public ConsoleUploadObserver(ConsoleUi ui, ProgressContext? context = null)
    {
        _ui = ui;
        _context = context;
    }

    /// <inheritdoc />
    public void PlanReady(UploadPlan plan)
    {
        var work = plan.Work.Count();
        var skipped = plan.CountOf(UploadAction.Skip);
        var blocked = plan.Blocked.Count();

        if (_context is null)
        {
            _ui.Info($"{work} to upload, {skipped} skipped, {blocked} blocked");
        }
    }

    /// <inheritdoc />
    public void ItemStarted(UploadPlanItem item)
    {
        lock (_gate)
        {
            if (_context is null)
            {
                _ui.Info($"{item.File}: {Describe(item)}");
                return;
            }

            if (_tasks.ContainsKey(item.File))
            {
                return;
            }

            var task = _context.AddTask(
                Spectre.Console.Markup.Escape(Path.GetFileName(item.File)),
                autoStart: false,
                maxValue: Math.Max(item.FileSize, 1));

            task.IsIndeterminate = item.FileSize <= 0;
            _tasks[item.File] = task;
        }
    }

    /// <inheritdoc />
    public void ItemProgress(UploadPlanItem item, long bytesSent)
    {
        lock (_gate)
        {
            if (!_tasks.TryGetValue(item.File, out var task))
            {
                return;
            }

            task.IsIndeterminate = false;
            task.Value = Math.Clamp(bytesSent, 0, (long)task.MaxValue);
        }
    }

    /// <inheritdoc />
    public void ItemFinished(UploadOutcome outcome)
    {
        lock (_gate)
        {
            if (_context is null)
            {
                Report(outcome);
                return;
            }

            if (_tasks.Remove(outcome.Item.File, out var task))
            {
                if (outcome.Succeeded)
                {
                    task.Value = task.MaxValue;
                }

                task.Description = Spectre.Console.Markup.Escape(
                    $"{Path.GetFileName(outcome.Item.File)} {Mark(outcome)}");
            }
        }
    }

    /// <summary>Writes the result of one item.</summary>
    public void Report(UploadOutcome outcome)
    {
        var name = Path.GetFileName(outcome.Item.File);

        if (!outcome.Succeeded)
        {
            _ui.Error($"{name}: {outcome.Error}");
        }
        else
        {
            _ui.Success($"{name}: {Mark(outcome)}");
        }

        foreach (var note in outcome.Notes.Where(note => note.StartsWith("manual:", StringComparison.Ordinal)))
        {
            _ui.Manual(note["manual:".Length..].Trim());
        }

        foreach (var note in outcome.Notes.Where(note => !note.StartsWith("manual:", StringComparison.Ordinal)))
        {
            _ui.Detail(note);
        }
    }

    private static string Mark(UploadOutcome outcome) => outcome.Status switch
    {
        UploadOutcomeStatus.Uploaded => "uploaded",
        UploadOutcomeStatus.Resumed => "continued",
        UploadOutcomeStatus.Updated => "updated",
        _ => "failed",
    };

    private static string Describe(UploadPlanItem item) => item.Action switch
    {
        UploadAction.Upload => "uploading",
        UploadAction.Resume => $"continuing at {item.BytesSent:N0} bytes",
        UploadAction.ApplyExtras => "adding thumbnail and playlists",
        _ => item.Reason ?? "done",
    };
}