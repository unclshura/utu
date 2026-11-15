using System.Text.Json;
using Utu;
using Utu.Configuration;

namespace Utu.Tests;

[TestClass]
public sealed class ConfigIOTests
{
    [TestMethod]
    public void Parse_reads_the_documented_document_shape()
    {
        var config = ConfigIO.Parse(
            """
            {
              "$schema": "https://example.invalid/utu.schema.json",
              // comments and trailing commas are allowed
              "channel": {
                "name": "My channel",
                "clientSecretsFile": "secret.json",
                "parallelUploads": 2,
              },
              "defaults": { "category": "Music", "privacy": "private" },
              "videos": [
                { "file": "First Song.mp4", "title": "First Song", "tags": ["a", "b"] },
                { "file": "second.mp4", "enabled": false, "privacy": "unlisted" },
              ],
            }
            """);

        Assert.AreEqual("My channel", config.Channel.Name);
        Assert.AreEqual("secret.json", config.Channel.ClientSecretsFile);
        Assert.AreEqual(2, config.Channel.ParallelUploads);
        Assert.AreEqual("Music", config.Defaults.Category);
        Assert.AreEqual(Privacy.Private, config.Defaults.Privacy);
        Assert.AreEqual(2, config.Videos.Count);
        Assert.AreEqual("First Song.mp4", config.Videos[0].File);
        CollectionAssert.AreEqual(new[] { "a", "b" }, config.Videos[0].Tags!.ToArray());
        Assert.IsFalse(config.Videos[1].IsEnabled);
        Assert.AreEqual(Privacy.Unlisted, config.Videos[1].Privacy);
    }

    [TestMethod]
    public void Parse_is_case_insensitive_for_member_names()
    {
        var config = ConfigIO.Parse("""{ "VIDEOS": [ { "FILE": "a.mp4", "MADEFORKIDS": true } ] }""");

        Assert.AreEqual("a.mp4", config.Videos[0].File);
        Assert.IsTrue(config.Videos[0].MadeForKids);
    }

    [TestMethod]
    public void Parse_rejects_unknown_members()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => ConfigIO.Parse("""{ "videos": [], "bogus": 1 }"""));

        Assert.AreEqual(2, exception.ExitCode);
        StringAssert.Contains(exception.Message, "bogus");
    }

    [TestMethod]
    public void Parse_rejects_invalid_json_and_empty_documents()
    {
        Assert.ThrowsExactly<UtuException>(() => ConfigIO.Parse("{ nope"));
        Assert.ThrowsExactly<UtuException>(() => ConfigIO.Parse("null"));
    }

    [TestMethod]
    public void Serialize_round_trips_every_setting()
    {
        var config = new UtuConfig
        {
            Channel = new ChannelConfig { Name = "Channel", ParallelUploads = 5 },
            Defaults = new VideoSettings { Category = "Music", License = VideoLicense.CreativeCommon },
            Videos =
            [
                new VideoEntry
                {
                    File = "a.mp4",
                    Title = "Title",
                    Privacy = Privacy.Unlisted,
                    RecordingDate = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero),
                    Location = "48.8584,2.2945",
                    PublishAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
                    Playlists = ["Songs"],
                    Localizations = new Dictionary<string, VideoLocalization> { ["fr"] = new VideoLocalization { Title = "Titre" } },
                    Comments = CommentPolicy.Off,
                },
            ],
        };

        var roundTripped = ConfigIO.Parse(ConfigIO.Serialize(config));

        Assert.AreEqual("Channel", roundTripped.Channel.Name);
        Assert.AreEqual(VideoLicense.CreativeCommon, roundTripped.Defaults.License);
        var video = roundTripped.Videos[0];
        Assert.AreEqual("Title", video.Title);
        Assert.AreEqual(Privacy.Unlisted, video.Privacy);
        Assert.AreEqual(new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero), video.RecordingDate);
        Assert.AreEqual("48.8584,2.2945", video.Location);
        Assert.AreEqual(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), video.PublishAt);
        CollectionAssert.AreEqual(new[] { "Songs" }, video.Playlists!.ToArray());
        Assert.AreEqual("Titre", video.Localizations!["fr"].Title);
        Assert.AreEqual(CommentPolicy.Off, video.Comments);
    }

    [TestMethod]
    public void Serialize_writes_placeholders_but_no_computed_properties()
    {
        var json = ConfigIO.Serialize(new UtuConfig { Videos = [new VideoEntry { File = "a.mp4" }] });

        StringAssert.Contains(json, "\"enabled\": null");
        Assert.IsFalse(json.Contains("isEnabled", StringComparison.Ordinal), json);
        Assert.IsFalse(json.Contains("identity", StringComparison.OrdinalIgnoreCase), json);
    }

    [TestMethod]
    public void Enums_are_written_in_camel_case()
    {
        var json = ConfigIO.Serialize(new UtuConfig { Defaults = new VideoSettings { Privacy = Privacy.Unlisted } });

        StringAssert.Contains(json, "\"unlisted\"");
    }

    [TestMethod]
    public void Load_reports_a_missing_file_and_reads_an_existing_one()
    {
        using var folder = new TempFolder();
        var path = folder["utu.json"];

        var missing = Assert.ThrowsExactly<UtuException>(() => ConfigIO.Load(path));
        StringAssert.Contains(missing.Message, "not found");

        ConfigIO.Save(path, TestConfig.With(TestConfig.Entry("a.mp4")));
        Assert.IsTrue(File.Exists(path));

        var loaded = ConfigIO.Load(path);
        Assert.AreEqual("a.mp4", loaded.Videos[0].File);
        Assert.IsTrue(File.ReadAllText(path).EndsWith(Environment.NewLine, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Save_creates_missing_directories()
    {
        using var folder = new TempFolder();
        var path = folder["nested/deeper/utu.json"];

        ConfigIO.Save(path, new UtuConfig());

        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void Options_are_shared_with_the_state_file()
    {
        Assert.AreEqual(JsonNamingPolicy.CamelCase, ConfigIO.Options.PropertyNamingPolicy);
        Assert.AreEqual("utu.json", ConfigIO.DefaultFileName);
    }
}
