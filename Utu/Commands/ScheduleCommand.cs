using Utu.Cli;
using Utu.Configuration;
using Utu.Core;

namespace Utu.Commands;

/// <summary>
/// Recomputes the publication time of every video from the
/// <c>publishSchedule</c> of the configuration.
/// </summary>
public static class ScheduleCommand
{
    /// <summary>Runs <c>utu schedule</c>.</summary>
    /// <param name="line">Parsed command line.</param>
    /// <param name="ui">Console to write to.</param>
    /// <param name="homeFolder">Replaces the home folder of the user; for tests.</param>
    /// <param name="overrideFolder">Replaces <see cref="ConfigLocator.HomeVariable" />; for tests.</param>
    /// <exception cref="UtuException">There is no usable publication schedule.</exception>
    public static int Run(
        CommandLine line,
        ConsoleUi ui,
        string? homeFolder = null,
        string? overrideFolder = null)
    {
        var root = Path.GetFullPath(line.Folder);
        var globalConfigPath = ConfigurationLoader.GlobalConfigPath(homeFolder, overrideFolder);

        ui.Heading("utu schedule");

        var loaded = ConfigurationLoader.Load(
            root,
            line.ConfigFile,
            homeFolder: homeFolder,
            overrideFolder: overrideFolder);

        ui.Info($"configuration: {loaded.ConfigPath}");
        ui.Info(loaded.GlobalPath is null
            ? $"global: {globalConfigPath} (not found, no defaults are inherited)"
            : $"global: {loaded.GlobalPath} (defaults are inherited from it)");

        var expression = loaded.Resolved.PublishSchedule;

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new UtuException(
                $"no 'publishSchedule' is configured; add it to the 'defaults' of '{loaded.ConfigPath}' or of " +
                $"'{globalConfigPath}'",
                CommandLineParser.UsageExitCode);
        }

        if (!CronSchedule.TryParse(expression, out _, out var error))
        {
            throw new UtuException(
                $"'publishSchedule' is not a valid crontab expression: {error}",
                CommandLineParser.UsageExitCode);
        }

        var config = loaded.FolderConfig;
        var videos = config.Videos ?? [];

        if (videos.Count == 0)
        {
            ui.Warning($"'{loaded.ConfigPath}' has no videos; run 'utu init' first");
            return 1;
        }

        // The videos keep the order of the configuration, which is the order in
        // which 'utu init' wrote them and which the file can rearrange by hand.
        var previous = videos.Select(entry => entry.PublishAt).ToList();
        var changed = PublishScheduler.Replace(videos, expression, DateTimeOffset.Now);

        ReportWriter.WriteSchedule(ui, expression, videos);
        ReportWriter.WriteScheduleWarnings(ui, loaded.Resolved, videos);
        ReportWriter.WritePublicationTimes(ui, videos, previous);

        if (line.DryRun)
        {
            ui.Blank();
            ui.Success($"dry run: {changed} of {videos.Count} publication times would change, nothing was written");
            return 0;
        }

        ConfigIO.SaveWithPlaceholders(loaded.ConfigPath, config);

        ui.Blank();
        ui.Success($"'{loaded.ConfigPath}' written: {changed} of {videos.Count} publication times changed");
        ui.Info("the videos are published in the order of the file; run 'utu upload' to apply the times");

        return 0;
    }
}
