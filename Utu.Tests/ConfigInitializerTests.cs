using System.Text.Json;
using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class ConfigInitializerTests
{
    [TestMethod]
    public void Discovered_videos_are_added_with_a_guessed_title()
    {
        using var folder = new TempFolder();
        folder.WriteFile("01 - My_song.mp4");
        folder.WriteFile("notes.txt");
        folder.WriteFile("sub/other.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null);

        Assert.AreEqual(1, result.Added.Count);
        Assert.AreEqual(1, result.Config.Videos.Count);
        Assert.AreEqual("01 - My_song.mp4", result.Config.Videos[0].File);
        Assert.AreEqual("01 My song", result.Config.Videos[0].Title);
        Assert.IsNull(result.Added[0].Thumbnail);
        Assert.IsTrue(result.HasChanges);
    }

    [TestMethod]
    public void Sub_folders_are_searched_when_asked()
    {
        using var folder = new TempFolder();
        folder.WriteFile("sub/other.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null, new InitOptions { Recursive = true });

        Assert.AreEqual("sub/other.mp4", result.Config.Videos.Single().File);
    }

    [TestMethod]
    public void A_thumbnail_next_to_the_video_is_detected()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("a.jpg");

        var result = ConfigInitializer.Create(folder.Path, existing: null);

        Assert.AreEqual("a.jpg", result.Added.Single().Thumbnail);
        Assert.AreEqual("a.jpg", result.Config.Videos.Single().Thumbnail);
    }

    [TestMethod]
    public void The_videos_are_sorted_by_name()
    {
        using var folder = new TempFolder();
        folder.WriteFile("b.mp4");
        folder.WriteFile("a.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null);

        CollectionAssert.AreEqual(new[] { "a.mp4", "b.mp4" }, result.Config.Videos.Select(entry => entry.File).ToArray());
    }

    [TestMethod]
    public void Configured_videos_are_kept_and_missing_files_are_reported()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var existing = TestConfig.With(
            new VideoEntry { File = "a.mp4", Title = "My own title" },
            new VideoEntry { File = "gone.mp4", Title = "Gone" });

        var result = ConfigInitializer.Create(folder.Path, existing);

        Assert.AreEqual(0, result.Added.Count);
        Assert.AreEqual("a.mp4", result.Kept.Single().File);
        CollectionAssert.AreEqual(new[] { "gone.mp4" }, result.MissingFiles.ToArray());
        Assert.IsTrue(result.HasChanges);
        Assert.AreEqual("My own title", result.Config.Videos.Single(entry => entry.File == "a.mp4").Title);
    }

    [TestMethod]
    public void Force_replaces_the_existing_configuration()
    {
        using var folder = new TempFolder();
        folder.WriteFile("b.mp4");
        var existing = TestConfig.With(new VideoEntry { File = "a.mp4", Title = "My own title" });

        var result = ConfigInitializer.Create(folder.Path, existing, new InitOptions { Force = true });

        Assert.AreEqual("b.mp4", result.Config.Videos.Single().File);
        Assert.AreEqual("b", result.Config.Videos.Single().Title);
        CollectionAssert.AreEqual(new[] { "b.mp4" }, result.Added.Select(entry => entry.File).ToArray());
    }

    [TestMethod]
    public void New_videos_only_set_the_category_so_the_rest_can_be_inherited()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null, new InitOptions { Category = "Rock" });

        var defaults = result.Config.Defaults;
        Assert.AreEqual("Rock", defaults.Category);
        Assert.IsNull(defaults.Privacy);
        Assert.IsNull(defaults.License);
        Assert.IsNull(defaults.MadeForKids);
        Assert.IsNull(defaults.Embeddable);
        Assert.IsNull(defaults.ShowLikeCount);
        Assert.IsNull(defaults.AiUse);
    }

    [TestMethod]
    public void Without_a_category_every_default_stays_a_placeholder()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null);

        Assert.IsNull(result.Config.Defaults.Category);
        Assert.IsTrue(IsEmpty(result.Config.Defaults), "a default has a value");
    }

    [TestMethod]
    public void The_written_configuration_names_every_setting_as_a_placeholder()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var path = folder["utu.json"];

        ConfigIO.SaveWithPlaceholders(path, ConfigInitializer.Create(folder.Path, existing: null).Config);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var defaults = document.RootElement.GetProperty("defaults");
        var written = defaults.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);

        var missing = SettingNames<DefaultsSettings>().Where(name => !written.ContainsKey(name)).ToList();
        Assert.AreEqual(0, missing.Count, $"the placeholders lack: {string.Join(", ", missing)}");

        foreach (var (name, value) in written)
        {
            Assert.AreEqual(JsonValueKind.Null, value.ValueKind, $"'{name}' is not a placeholder");
        }
    }

    [TestMethod]
    public void The_category_given_to_init_fills_its_placeholder()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var path = folder["utu.json"];

        var result = ConfigInitializer.Create(folder.Path, existing: null, new InitOptions { Category = "Rock" });
        ConfigIO.SaveWithPlaceholders(path, result.Config);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var category = document.RootElement.GetProperty("defaults").GetProperty("category");

        Assert.AreEqual(JsonValueKind.String, category.ValueKind);
        Assert.AreEqual("Rock", category.GetString());
    }

    [TestMethod]
    public void The_written_videos_carry_no_placeholders()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("a.jpg");
        var path = folder["utu.json"];

        ConfigIO.SaveWithPlaceholders(path, ConfigInitializer.Create(folder.Path, existing: null).Config);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var video = document.RootElement.GetProperty("videos")[0];
        var written = video.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "file", "thumbnail", "title" }, written);
    }

    [TestMethod]
    public void The_placeholders_inherit_the_built_in_values()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var path = folder["utu.json"];

        ConfigIO.SaveWithPlaceholders(path, ConfigInitializer.Create(folder.Path, existing: null).Config);

        var resolved = ResolvedConfiguration.Resolve(folder.Path, ConfigIO.Load(path));
        var builtIn = SettingsResolver.Resolve(new VideoEntry(), defaults: null);

        Assert.IsNull(resolved.Defaults.Category);
        Assert.AreEqual(Privacy.Private, SettingsResolver.Resolve(new VideoEntry(), resolved.Defaults).Privacy);
        Assert.AreEqual(VideoLicense.Youtube, SettingsResolver.Resolve(new VideoEntry(), resolved.Defaults).License);
        Assert.AreEqual(builtIn.MadeForKids, SettingsResolver.Resolve(new VideoEntry(), resolved.Defaults).MadeForKids);
        Assert.AreEqual(builtIn.Embeddable, SettingsResolver.Resolve(new VideoEntry(), resolved.Defaults).Embeddable);
    }

    [TestMethod]
    public void The_defaults_of_an_existing_configuration_are_left_alone()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var existing = TestConfig.WithDefaults(
            new DefaultsSettings { Category = "Music", Privacy = Privacy.Unlisted },
            new VideoEntry { File = "a.mp4", Title = "T" });

        var result = ConfigInitializer.Create(folder.Path, existing, new InitOptions { Category = "Rock" });

        Assert.AreEqual("Music", result.Config.Defaults.Category);
        Assert.AreEqual(Privacy.Unlisted, result.Config.Defaults.Privacy);
    }

    [TestMethod]
    public void A_missing_folder_is_an_error()
    {
        using var folder = new TempFolder();
        var missing = folder["nope"];

        var exception = Assert.ThrowsExactly<UtuException>(() => ConfigInitializer.Create(missing, existing: null));

        Assert.AreEqual(1, exception.ExitCode);
        StringAssert.Contains(exception.Message, "does not exist");
    }

    [TestMethod]
    [DataRow("a.mp4", "a")]
    [DataRow("01 - My_song.v2.mp4", "01 My song v2")]
    [DataRow("no_extension", "no extension")]
    [DataRow("UPPER.MP4", "UPPER")]
    public void GuessTitle_reads_a_title_from_the_file_name(string file, string expected)
    {
        Assert.AreEqual(expected, ConfigInitializer.GuessTitle(file));
    }

    [TestMethod]
    public void GuessTitle_stops_at_the_title_limit()
    {
        var title = ConfigInitializer.GuessTitle(new string('a', 150) + ".mp4");

        Assert.AreEqual(VideoMetadataFactory.MaxTitleLength, title.Length);
    }

    [TestMethod]
    public void A_publish_schedule_spreads_the_publication_times_over_the_videos()
    {
        using var folder = new TempFolder();
        folder.WriteFile("b.mp4");
        folder.WriteFile("a.mp4");
        folder.WriteFile("c.mp4");

        var now = new DateTimeOffset(2024, 1, 1, 10, 30, 45, TimeSpan.Zero);
        var result = ConfigInitializer.Create(
            folder.Path,
            existing: null,
            new InitOptions { PublishSchedule = "0 * * * *" },
            now);

        var expected = CronSchedule
            .Parse("0 * * * *", TimeZoneInfo.Local)
            .Occurrences(now)
            .Take(3)
            .Select(moment => (DateTimeOffset?)moment)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "a.mp4", "b.mp4", "c.mp4" },
            result.Config.Videos.Select(entry => entry.File).ToArray());

        CollectionAssert.AreEqual(expected, result.Config.Videos.Select(entry => entry.PublishAt).ToList());
    }

    [TestMethod]
    public void Without_a_publish_schedule_nothing_is_scheduled()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null);

        Assert.IsNull(result.Config.Videos[0].PublishAt, "a video without a schedule");
    }

    [TestMethod]
    public void A_video_that_already_has_a_publication_time_keeps_it()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("b.mp4");

        var fixedTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var existing = TestConfig.With(new VideoEntry { File = "a.mp4", Title = "A", PublishAt = fixedTime });

        var result = ConfigInitializer.Create(
            folder.Path,
            existing,
            new InitOptions { PublishSchedule = "@daily" },
            new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(fixedTime, result.Config.Videos[0].PublishAt, "the time of an existing video");
        Assert.IsNotNull(result.Config.Videos[1].PublishAt, "a video that has none");
    }

    [TestMethod]
    public void An_invalid_publish_schedule_is_reported()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var exception = Assert.ThrowsExactly<UtuException>(() => ConfigInitializer.Create(
            folder.Path,
            existing: null,
            new InitOptions { PublishSchedule = "every hour" }));

        Assert.AreEqual(2, exception.ExitCode);
        StringAssert.Contains(exception.Message, "crontab");
    }

    [TestMethod]
    public void A_publish_schedule_that_never_matches_is_reported()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var exception = Assert.ThrowsExactly<UtuException>(() => ConfigInitializer.Create(
            folder.Path,
            existing: null,
            new InitOptions { PublishSchedule = "0 0 30 2 *" }));

        Assert.AreEqual(2, exception.ExitCode);
        StringAssert.Contains(exception.Message, "0 0 30 2 *");
    }

    private static IEnumerable<string> SettingNames<T>() =>
        typeof(T)
            .GetProperties()
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name));

    private static bool IsEmpty(VideoSettings settings) =>
        typeof(VideoSettings)
            .GetProperties()
            .All(property => property.GetValue(settings) is null);
}
