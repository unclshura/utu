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
    public void New_videos_use_the_utu_defaults()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");

        var result = ConfigInitializer.Create(folder.Path, existing: null, new InitOptions { Category = "Rock" });

        var defaults = result.Config.Defaults;
        Assert.AreEqual("Rock", defaults.Category);
        Assert.AreEqual(Privacy.Private, defaults.Privacy);
        Assert.AreEqual(VideoLicense.Youtube, defaults.License);
        Assert.IsFalse(defaults.MadeForKids);
        Assert.IsTrue(defaults.Embeddable);
        Assert.IsTrue(defaults.ShowLikeCount);
        Assert.IsFalse(defaults.AiUse);
    }

    [TestMethod]
    public void The_defaults_of_an_existing_configuration_are_left_alone()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var existing = TestConfig.WithDefaults(
            new VideoSettings { Category = "Music", Privacy = Privacy.Unlisted },
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
}
