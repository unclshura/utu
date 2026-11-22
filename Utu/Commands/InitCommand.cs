using Spectre.Console;
using Utu.Cli;
using Utu.Configuration;
using Utu.Core;

namespace Utu.Commands;

/// <summary>Creates or extends <c>utu.json</c> from the videos of a folder.</summary>
public static class InitCommand
{
    /// <summary>Runs <c>utu init</c>.</summary>
    public static int Run(CommandLine line, ConsoleUi ui)
    {
        var root = Path.GetFullPath(line.Folder);
        var configPath = PathResolver.Resolve(root, line.ConfigFile ?? ConfigIO.DefaultFileName);
        var category = line.Category;

        if (category is not null && CategoryRegistry.Resolve(category) is null)
        {
            throw new UtuException(
                $"unknown category '{category}'; known categories are {CategoryRegistry.DescribeKnownNames()}",
                CommandLineParser.UsageExitCode);
        }

        ui.Heading("utu init");
        ui.Info($"folder: {root}");
        ui.Info($"configuration: {configPath}");

        var globalConfigPath = ConfigurationLoader.GlobalConfigPath();
        var createdGlobal = !SamePath(globalConfigPath, configPath) &&
            GlobalConfiguration.EnsureExists(globalConfigPath);

        var globalPath = ConfigurationLoader.FindGlobalConfig(configPath, line.ConfigFile);
        if (createdGlobal)
        {
            ui.Info($"global: created '{globalConfigPath}' with all settings and their built in values");
            ui.Detail("channel settings and default video parameters are inherited from it; empty values mean 'use the built in default'");
        }
        else if (globalPath is not null)
        {
            ui.Info($"global: {globalPath} (channel and defaults are inherited from it)");
        }
        else if (!string.IsNullOrWhiteSpace(line.ConfigFile))
        {
            ui.Info($"global: '{globalConfigPath}' is not used because '--config' names an exact file");
        }
        else
        {
            ui.Info($"global: none; '{globalConfigPath}' is used for channel and default settings");
        }

        UtuConfig? existing = null;
        if (File.Exists(configPath) && !line.Force)
        {
            existing = ConfigIO.Load(configPath);
            ui.Info("adding to the existing configuration");
        }

        var globalConfig = globalPath is null ? null : ConfigIO.Load(globalPath);
        var resolved = ResolvedConfiguration.Resolve(
            root,
            existing ?? new UtuConfig(),
            globalConfig,
            globalPath is null ? null : Path.GetDirectoryName(globalPath));

        var defaults = new InitOptions
        {
            Recursive = line.Recursive,
            Force = line.Force,
            Category = category,
            PublishSchedule = resolved.PublishSchedule,
        };

        var result = ConfigInitializer.Create(root, existing, defaults);
        ConfigIO.SaveWithPlaceholders(configPath, result.Config);

        if (globalPath is not null)
        {
            ui.Detail($"channel: {(resolved.Channel.Name ?? "(unnamed)")}, {resolved.Channel.ParallelUploads} parallel uploads");
            ui.Detail($"client secrets: {resolved.Channel.ClientSecretsFile}");
            ui.Detail($"token cache: {resolved.Channel.TokenCacheDirectory}");
        }

        WriteEntries(ui, "Videos", result.Added, verboseOnly: false);
        WriteEntries(ui, "Already configured", result.Kept, verboseOnly: true);

        WriteSchedule(ui, resolved, result.Config.Videos);

        foreach (var file in result.MissingFiles)
        {
            ui.Warning($"'{file}' is configured but does not exist");
        }

        ui.Blank();

        if (!result.HasChanges)
        {
            ui.Success($"'{configPath}' is up to date ({result.Kept.Count} videos)");
        }
        else
        {
            ui.Success($"'{configPath}' written: {result.Added.Count} new, {result.Kept.Count} known, {result.MissingFiles.Count} missing");
        }

        ui.Info("Edit the titles, descriptions and thumbnails, then run 'utu upload'.");
        ui.Info("the 'defaults' block of the file names every setting: null means 'inherit', a value applies to every video");

        if (createdGlobal)
        {
            ui.Info($"Edit '{globalConfigPath}' to change the channel and the default video parameters.");
        }

        return 0;
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>Reports the publication times the schedule produced.</summary>
    private static void WriteSchedule(ConsoleUi ui, ResolvedConfiguration resolved, IReadOnlyList<VideoEntry> videos)
    {
        if (string.IsNullOrWhiteSpace(resolved.PublishSchedule))
        {
            return;
        }

        ReportWriter.WriteSchedule(ui, resolved.PublishSchedule, videos);

        // YouTube only accepts a publication time on a private video; a public or
        // unlisted video with a publication time is rejected at upload.
        if (SettingsResolver.Resolve(new VideoEntry(), resolved.Defaults).Privacy is not Privacy.Private)
        {
            ui.Warning("a publication time needs 'privacy: private' in the defaults; YouTube rejects the video otherwise");
        }
    }

    private static void WriteEntries(ConsoleUi ui, string title, IReadOnlyList<InitEntry> entries, bool verboseOnly)
    {
        if (entries.Count == 0 || (verboseOnly && !ui.IsVerbose))
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(new TableTitle(title))
            .AddColumn(new TableColumn("Video"))
            .AddColumn(new TableColumn("Thumbnail"));

        foreach (var entry in entries)
        {
            table.AddRow(new Text(entry.File), new Text(entry.Thumbnail ?? "-"));
        }

        ui.Write(table);
    }
}
