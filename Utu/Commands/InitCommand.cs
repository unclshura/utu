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

        var defaults = new InitOptions
        {
            Recursive = line.Recursive,
            Force = line.Force,
            Category = category is null ? "Music" : category,
        };

        ui.Heading("utu init");
        ui.Info($"folder: {root}");
        ui.Info($"configuration: {configPath}");

        UtuConfig? existing = null;
        if (File.Exists(configPath) && !line.Force)
        {
            existing = ConfigIO.Load(configPath);
            ui.Info("adding to the existing configuration");
        }

        var result = ConfigInitializer.Create(root, existing, defaults);
        ConfigIO.Save(configPath, result.Config);

        WriteEntries(ui, "Videos", result.Added, verboseOnly: false);
        WriteEntries(ui, "Already configured", result.Kept, verboseOnly: true);

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

        return 0;
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