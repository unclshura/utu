using Utu.Configuration;

namespace Utu.Tests;

[TestClass]
public sealed class SettingsResolverTests
{
    [TestMethod]
    public void Entry_values_win_over_defaults()
    {
        var defaults = new VideoSettings { Title = "from defaults", Description = "description", Tags = ["default"] };
        var entry = TestConfig.Entry("a.mp4", "from entry");

        var resolved = SettingsResolver.Resolve(entry, defaults);

        Assert.AreEqual("from entry", resolved.Title);
        Assert.AreEqual("description", resolved.Description);
    }

    [TestMethod]
    public void Lists_are_taken_from_the_entry_as_a_whole()
    {
        var defaults = new VideoSettings { Tags = ["default"], Playlists = ["Default list"] };
        var entry = TestConfig.Entry("a.mp4");
        entry.Tags = ["own"];

        var resolved = SettingsResolver.Resolve(entry, defaults);

        CollectionAssert.AreEqual(new[] { "own" }, resolved.Tags!.ToArray());
        CollectionAssert.AreEqual(new[] { "Default list" }, resolved.Playlists!.ToArray());
    }

    [TestMethod]
    public void Utu_defaults_are_applied_to_unset_values()
    {
        var resolved = SettingsResolver.Resolve(TestConfig.Entry("a.mp4"), defaults: null);

        Assert.AreEqual(Privacy.Private, resolved.Privacy);
        Assert.AreEqual(VideoLicense.Youtube, resolved.License);
        Assert.IsTrue(resolved.Embeddable);
        Assert.IsTrue(resolved.ShowLikeCount);
        Assert.IsFalse(resolved.AiUse);
        Assert.IsFalse(resolved.MadeForKids);
    }

    [TestMethod]
    public void Explicit_values_are_kept()
    {
        var entry = TestConfig.Entry("a.mp4");
        entry.Privacy = Privacy.Public;
        entry.License = VideoLicense.CreativeCommon;
        entry.Embeddable = false;
        entry.ShowLikeCount = false;
        entry.AiUse = true;

        var resolved = SettingsResolver.Resolve(entry, defaults: null);

        Assert.AreEqual(Privacy.Public, resolved.Privacy);
        Assert.AreEqual(VideoLicense.CreativeCommon, resolved.License);
        Assert.IsFalse(resolved.Embeddable);
        Assert.IsFalse(resolved.ShowLikeCount);
        Assert.IsTrue(resolved.AiUse);
    }

    [TestMethod]
    public void Entry_settings_do_not_contain_the_file()
    {
        var entry = TestConfig.Entry("a.mp4");

        var settings = entry.ToSettings();

        Assert.AreEqual("a", settings.Title);
        Assert.IsFalse(settings is VideoEntry);
    }

    [TestMethod]
    public void Clone_is_a_deep_copy()
    {
        var settings = new VideoSettings { Tags = ["a"], Localizations = new Dictionary<string, VideoLocalization> { ["fr"] = new VideoLocalization { Title = "Titre" } } };

        var clone = settings.Clone();
        clone.Tags!.Add("b");
        clone.Localizations!["fr"].Title = "changed";

        CollectionAssert.AreEqual(new[] { "a" }, settings.Tags!.ToArray());
        Assert.AreEqual("Titre", settings.Localizations!["fr"].Title);
    }
}
