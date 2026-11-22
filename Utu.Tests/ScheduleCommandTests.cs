using Spectre.Console;
using Utu.Cli;
using Utu.Commands;
using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class ScheduleCommandTests
{
    [TestMethod]
    public void The_times_of_all_videos_are_recomputed()
    {
        using var folder = new TempFolder();
        folder.WriteFile(
            ConfigIO.DefaultFileName,
            """
            {
              "defaults": { "publishSchedule": "0 * * * *", "privacy": "private" },
              "videos": [
                { "file": "a.mp4", "title": "A", "publishAt": "2001-01-01T00:00:00+00:00" },
                { "file": "b.mp4", "title": "B" }
              ]
            }
            """);
        var (ui, output) = CreateUi();

        var before = DateTimeOffset.Now;
        var exitCode = ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: "");
        var after = DateTimeOffset.Now;

        Assert.AreEqual(0, exitCode, output());

        var videos = ConfigIO.Load(folder[ConfigIO.DefaultFileName]).Videos!;
        Assert.AreEqual(2, videos.Count);
        Assert.IsTrue(videos[0].PublishAt > before, "the first time is in the future");
        Assert.IsTrue(videos[0].PublishAt <= after.AddHours(1), "the first time is the next hour");
        Assert.AreEqual(videos[0].PublishAt!.Value.AddHours(1), videos[1].PublishAt, "the times are an hour apart");

        StringAssert.Contains(output(), "publish schedule '0 * * * *'");
        StringAssert.Contains(output(), "Publication times");
        StringAssert.Contains(output(), $"{folder[ConfigIO.DefaultFileName]}' written", "the file was written");
    }

    [TestMethod]
    public void A_dry_run_leaves_the_file_untouched()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile(
            ConfigIO.DefaultFileName,
            """
            {
              "defaults": { "publishSchedule": "@daily", "privacy": "private" },
              "videos": [ { "file": "a.mp4", "title": "A", "publishAt": "2001-01-01T00:00:00+00:00" } ]
            }
            """);
        var (ui, output) = CreateUi();

        var exitCode = ScheduleCommand.Run(
            Line(folder, dryRun: true), ui, homeFolder: folder.Path, overrideFolder: "");

        Assert.AreEqual(0, exitCode, output());
        StringAssert.Contains(output(), "dry run");
        StringAssert.Contains(output(), "2001-01-01", "the old time is reported");
        Assert.AreEqual(
            new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ConfigIO.Load(path).Videos![0].PublishAt);
    }

    [TestMethod]
    public void The_global_schedule_is_used_when_the_folder_has_none()
    {
        using var folder = new TempFolder();
        folder.WriteFile(ConfigIO.DefaultFileName, """{ "videos": [ { "file": "a.mp4" }, { "file": "b.mp4" } ] }""");
        folder.CreateDirectory(".utu");
        folder.WriteFile(
            @".utu\utu.json",
            """{ "defaults": { "publishSchedule": "@daily", "privacy": "private" } }""");
        var (ui, output) = CreateUi();

        var exitCode = ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: "");

        Assert.AreEqual(0, exitCode, output());

        var videos = ConfigIO.Load(folder[ConfigIO.DefaultFileName]).Videos!;
        Assert.IsTrue(videos[0].PublishAt > DateTimeOffset.Now, "the first time is in the future");
        Assert.AreEqual(videos[0].PublishAt!.Value.AddDays(1), videos[1].PublishAt, "the times are a day apart");
    }

    [TestMethod]
    public void An_explicit_configuration_file_is_used()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile(
            "other.json",
            """{ "defaults": { "publishSchedule": "@hourly" }, "videos": [ { "file": "a.mp4" } ] }""");
        var (ui, output) = CreateUi();

        var exitCode = ScheduleCommand.Run(
            Line(folder, config: "other.json"), ui, homeFolder: folder.Path, overrideFolder: "");

        Assert.AreEqual(0, exitCode, output());
        Assert.IsNotNull(ConfigIO.Load(path).Videos![0].PublishAt);
    }

    [TestMethod]
    public void Without_a_publish_schedule_the_command_fails()
    {
        using var folder = new TempFolder();
        folder.WriteFile(ConfigIO.DefaultFileName, """{ "videos": [ { "file": "a.mp4" } ] }""");
        var (ui, _) = CreateUi();

        var exception = Assert.ThrowsExactly<UtuException>(
            () => ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: ""));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "publishSchedule");
    }

    [TestMethod]
    public void An_invalid_publish_schedule_is_rejected()
    {
        using var folder = new TempFolder();
        folder.WriteFile(
            ConfigIO.DefaultFileName,
            """{ "defaults": { "publishSchedule": "every hour" }, "videos": [ { "file": "a.mp4" } ] }""");
        var (ui, _) = CreateUi();

        var exception = Assert.ThrowsExactly<UtuException>(
            () => ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: ""));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "crontab");
    }

    [TestMethod]
    public void A_schedule_that_names_too_few_times_is_rejected()
    {
        using var folder = new TempFolder();
        folder.WriteFile(
            ConfigIO.DefaultFileName,
            """
            {
              "defaults": { "publishSchedule": "0 0 30 2 *" },
              "videos": [ { "file": "a.mp4" }, { "file": "b.mp4" } ]
            }
            """);
        var (ui, _) = CreateUi();

        var exception = Assert.ThrowsExactly<UtuException>(
            () => ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: ""));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "too few times");
    }

    [TestMethod]
    public void A_configuration_without_videos_is_reported()
    {
        using var folder = new TempFolder();
        folder.WriteFile(ConfigIO.DefaultFileName, """{ "defaults": { "publishSchedule": "@daily" } }""");
        var (ui, output) = CreateUi();

        var exitCode = ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: "");

        Assert.AreEqual(1, exitCode, output());
        StringAssert.Contains(output(), "no videos");
    }

    [TestMethod]
    public void A_video_level_publish_schedule_is_rejected()
    {
        using var folder = new TempFolder();
        folder.WriteFile(
            ConfigIO.DefaultFileName,
            """{ "videos": [ { "file": "a.mp4", "publishSchedule": "@daily" } ] }""");
        var (ui, _) = CreateUi();

        var exception = Assert.ThrowsExactly<UtuException>(
            () => ScheduleCommand.Run(Line(folder), ui, homeFolder: folder.Path, overrideFolder: ""));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "publishSchedule");
    }

    private static CommandLine Line(TempFolder folder, bool dryRun = false, string? config = null) =>
        new()
        {
            Command = UtuCommand.Schedule,
            Folder = folder.Path,
            DryRun = dryRun,
            ConfigFile = config,
        };

    private static (ConsoleUi Ui, Func<string> Output) CreateUi()
    {
        var writer = new StringWriter();

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });

        console.Profile.Width = 100;

        return (new ConsoleUi(noColor: true, console: console), () => writer.ToString());
    }
}
