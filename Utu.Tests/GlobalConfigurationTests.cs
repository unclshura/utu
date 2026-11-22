using System.Text.Json;
using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class GlobalConfigurationTests
{
    [TestMethod]
    public void EnsureExists_writes_the_template_when_the_file_is_missing()
    {
        using var folder = new TempFolder();
        var path = folder[@"home\utu.json"];

        Assert.IsTrue(GlobalConfiguration.EnsureExists(path));
        Assert.IsTrue(File.Exists(path));

        var written = ConfigIO.Load(path);
        Assert.IsNotNull(written.Channel);
        Assert.AreEqual("client_secrets.json", written.Channel.ClientSecretsFile);
        Assert.AreEqual("auth", written.Channel.TokenCacheDirectory);
        Assert.AreEqual(ChannelSettings.DefaultParallelUploads, written.Channel.ParallelUploads);
        Assert.AreEqual(0, written.Videos.Count);
        Assert.AreEqual(string.Empty, written.Defaults.PublishSchedule);
    }

    [TestMethod]
    public void EnsureExists_keeps_an_existing_file()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("utu.json", "{ \"defaults\": { \"privacy\": \"unlisted\" } }");

        Assert.IsFalse(GlobalConfiguration.EnsureExists(path));
        Assert.AreEqual("{ \"defaults\": { \"privacy\": \"unlisted\" } }", File.ReadAllText(path));
    }

    [TestMethod]
    public void The_template_names_every_setting_utu_understands()
    {
        var json = ConfigIO.Serialize(GlobalConfiguration.CreateTemplate());

        var missing = new List<string>();

        missing.AddRange(
            Names<DefaultsSettings>()
                .Where(name => !json.Contains($"\"{name}\":", StringComparison.Ordinal)));

        missing.AddRange(
            Names<ChannelConfig>()
                .Where(name => !json.Contains($"\"{name}\":", StringComparison.Ordinal)));

        Assert.AreEqual(
            0,
            missing.Count,
            $"the template does not name: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void The_template_leaves_the_per_video_settings_out()
    {
        var json = ConfigIO.Serialize(GlobalConfiguration.CreateTemplate());

        foreach (var name in new[] { "recordingDate", "publishAt", "file", "enabled" })
        {
            Assert.IsFalse(json.Contains($"\"{name}\":", StringComparison.Ordinal), name);
        }
    }

    [TestMethod]
    public void The_template_round_trips_through_the_serializer()
    {
        var json = ConfigIO.Serialize(GlobalConfiguration.CreateTemplate());

        var jsonAgain = ConfigIO.Serialize(ConfigIO.Parse(json));

        Assert.AreEqual(json, jsonAgain);
    }

    [TestMethod]
    public void The_template_resolves_to_the_built_in_defaults()
    {
        using var folder = new TempFolder();
        var home = folder.CreateDirectory(".utu");
        var template = GlobalConfiguration.CreateTemplate();

        var withTemplate = ResolvedConfiguration.Resolve(folder.Path, new UtuConfig(), template, home, home);
        var builtIn = ResolvedConfiguration.Resolve(folder.Path, new UtuConfig(), homeFolder: home);

        Assert.IsNull(withTemplate.Channel.Name);
        Assert.AreEqual(builtIn.Channel.ClientSecretsFile, withTemplate.Channel.ClientSecretsFile);
        Assert.AreEqual(builtIn.Channel.TokenCacheDirectory, withTemplate.Channel.TokenCacheDirectory);
        Assert.AreEqual(builtIn.Channel.StateFile, withTemplate.Channel.StateFile);
        Assert.AreEqual(builtIn.Channel.User, withTemplate.Channel.User);
        Assert.AreEqual(builtIn.Channel.ParallelUploads, withTemplate.Channel.ParallelUploads);
        Assert.AreEqual(builtIn.Channel.ResumeUploads, withTemplate.Channel.ResumeUploads);

            AssertSameEffectiveSettings(
                SettingsResolver.Resolve(new VideoEntry(), builtIn.Defaults),
                SettingsResolver.Resolve(new VideoEntry(), withTemplate.Defaults),
                // YouTube Studio only settings have no value in the model; utu applies
                // them as documented follow ups, so the template spells them out.
                "allowAutomaticPlaces",
                "shortsRemixing",
                "comments",
                "moderation",
                "whoCanComment",
                // The category falls back to Music when it is not set.
                "category",
                // Declaring "no paid promotion" is the same as leaving it out.
                "paidPromotion");

                        Assert.AreEqual(false, withTemplate.Defaults.PaidPromotion);

                        Assert.AreEqual(
                CategoryRegistry.ResolveOrDefault(builtIn.Defaults.Category),
                CategoryRegistry.ResolveOrDefault(withTemplate.Defaults.Category));
                        Assert.AreEqual("Music", withTemplate.Defaults.Category);

                    Assert.AreEqual(true, withTemplate.Defaults.AllowAutomaticPlaces);
                    Assert.AreEqual(ShortsRemixing.VideoAndAudio, withTemplate.Defaults.ShortsRemixing);
                    Assert.AreEqual(CommentPolicy.On, withTemplate.Defaults.Comments);
                    Assert.AreEqual(CommentModeration.Basic, withTemplate.Defaults.Moderation);
                    Assert.AreEqual(CommentAudience.Everyone, withTemplate.Defaults.WhoCanComment);
    }

    [TestMethod]
    public void The_template_does_not_hide_the_settings_of_the_folder_configuration()
    {
        using var folder = new TempFolder();
        var home = folder.CreateDirectory(".utu");
        var template = GlobalConfiguration.CreateTemplate();
        var folderConfig = new UtuConfig
        {
            Defaults = new DefaultsSettings { Title = "Folder title", Privacy = Privacy.Public, Tags = ["folder"] },
            Videos = [TestConfig.Entry("song.mp4")],
        };

        var resolved = ResolvedConfiguration.Resolve(folder.Path, folderConfig, template, home, home);

        Assert.AreEqual("Folder title", resolved.Defaults.Title);
        Assert.AreEqual(Privacy.Public, resolved.Defaults.Privacy);
        CollectionAssert.AreEqual(new[] { "folder" }, resolved.Defaults.Tags);
    }

    [TestMethod]
    public void A_blank_channel_name_counts_as_unset()
    {
        using var folder = new TempFolder();
        var template = GlobalConfiguration.CreateTemplate();

        var resolved = ResolvedConfiguration.Resolve(
            folder.Path,
            new UtuConfig { Channel = new ChannelConfig { Name = string.Empty } },
            template);

        Assert.IsNull(resolved.Channel.Name);
    }

    private static IEnumerable<string> Names<T>() =>
        typeof(T)
            .GetProperties()
            .Where(property => property.PropertyType != typeof(DateTimeOffset?))
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name));

        /// <summary>Compares two settings objects, treating blank and empty collections as unset.</summary>
        private static void AssertSameEffectiveSettings(VideoSettings expected, VideoSettings actual, params string[] ignored)
        {
            var left = Properties(expected);
            var right = Properties(actual);

            foreach (var key in left.Keys.Union(right.Keys).Except(ignored).Order(StringComparer.Ordinal))
            {
                var first = left.GetValueOrDefault(key);
                var second = right.GetValueOrDefault(key);

                var same = IsUnset(first) && IsUnset(second) || first == second;
                Assert.IsTrue(same, $"{key}: '{second}' in the template but '{first}' built in");
            }
        }

        private static bool IsUnset(string? value) =>
            string.IsNullOrEmpty(value) || value is "[]" or "{}";

        private static Dictionary<string, string?> Properties(VideoSettings settings)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings, ConfigIO.Options));

            return document.RootElement
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => property.Value.GetString(),
                        _ => property.Value.GetRawText(),
                    });
        }
}