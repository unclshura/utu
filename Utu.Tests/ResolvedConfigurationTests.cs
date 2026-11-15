using Utu.Configuration;

namespace Utu.Tests;

[TestClass]
public sealed class ResolvedConfigurationTests
{
    [TestMethod]
    public void A_video_entry_beats_the_folder_defaults_which_beat_the_global_defaults()
    {
        var folder = new UtuConfig
        {
            Defaults = new VideoSettings { Category = "folder", Privacy = Privacy.Unlisted, License = VideoLicense.CreativeCommon },
            Videos = [new VideoEntry { File = "a.mp4", Category = "entry" }],
        };

        var global = new UtuConfig
        {
            Defaults = new VideoSettings
            {
                Category = "global",
                Privacy = Privacy.Public,
                License = VideoLicense.CreativeCommon,
                Description = "global description",
            },
        };

        var resolved = ResolvedConfiguration.Resolve(".", folder, global);

        Assert.AreEqual("folder", resolved.Defaults.Category);
        Assert.AreEqual(Privacy.Unlisted, resolved.Defaults.Privacy);
        Assert.AreEqual("global description", resolved.Defaults.Description);

        var settings = SettingsResolver.Resolve(resolved.Videos[0], resolved.Defaults);

        Assert.AreEqual("entry", settings.Category);
        Assert.AreEqual(Privacy.Unlisted, settings.Privacy);
        Assert.AreEqual(VideoLicense.CreativeCommon, settings.License);
        Assert.AreEqual("global description", settings.Description);
    }

    [TestMethod]
    public void Built_in_values_apply_when_no_layer_sets_a_setting()
    {
        var resolved = ResolvedConfiguration.Resolve(".", new UtuConfig());
        var settings = SettingsResolver.Resolve(new VideoEntry { File = "a.mp4" }, resolved.Defaults);

        Assert.AreEqual(Privacy.Private, settings.Privacy);
        Assert.AreEqual(VideoLicense.Youtube, settings.License);
        Assert.IsFalse(resolved.HasGlobal);
    }

    [TestMethod]
    public void Without_a_global_configuration_the_folder_values_are_used()
    {
        var folder = TestConfig.WithDefaults(new VideoSettings { Description = "folder" }, TestConfig.Entry("a.mp4"));

        var resolved = ResolvedConfiguration.Resolve(".", folder);

        Assert.AreEqual("folder", resolved.Defaults.Description);
        Assert.IsFalse(resolved.HasGlobal);
        Assert.AreEqual(1, resolved.Videos.Count);
    }

    [TestMethod]
    public void The_global_thumbnail_is_resolved_against_the_global_folder()
    {
        var global = new UtuConfig { Defaults = new VideoSettings { Thumbnail = "cover.jpg" } };

        var resolved = ResolvedConfiguration.Resolve(
            Path.GetTempPath(),
            new UtuConfig(),
            global,
            Path.GetPathRoot(Path.GetTempPath()));

        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(Path.GetPathRoot(Path.GetTempPath())!), "cover.jpg"),
            resolved.Defaults.Thumbnail);
    }

    [TestMethod]
    public void The_working_folder_is_made_absolute()
    {
        var resolved = ResolvedConfiguration.Resolve(".", new UtuConfig());

        Assert.AreEqual(Path.GetFullPath("."), resolved.WorkingFolder);
    }

    [TestMethod]
    public void A_null_folder_configuration_is_treated_as_empty()
    {
        var global = new UtuConfig { Defaults = new VideoSettings { Category = "global" } };

        var resolved = ResolvedConfiguration.Resolve(".", null, global);

        Assert.AreEqual("global", resolved.Defaults.Category);
        Assert.IsTrue(resolved.HasGlobal);
        Assert.AreEqual(0, resolved.Videos.Count);
    }
}

[TestClass]
public sealed class ChannelSettingsTests
{
    [TestMethod]
    public void A_folder_value_overrides_the_global_value()
    {
        var global = new ChannelConfig { Name = "global", ParallelUploads = 9, ResumeUploads = false, User = "global-user" };
        var folder = new ChannelConfig { Name = "folder", ParallelUploads = 2 };

        var channel = ChannelSettings.Resolve(folder, ".", global, ".");

        Assert.AreEqual("folder", channel.Name);
        Assert.AreEqual(2, channel.ParallelUploads);
        Assert.IsFalse(channel.ResumeUploads);
        Assert.AreEqual("global-user", channel.User);
    }

    [TestMethod]
    public void Built_in_values_apply_when_no_layer_sets_a_setting()
    {
        var channel = ChannelSettings.Resolve(null, Path.GetTempPath());

        Assert.IsNull(channel.Name);
        Assert.AreEqual(ChannelSettings.DefaultUser, channel.User);
        Assert.AreEqual(ChannelSettings.DefaultParallelUploads, channel.ParallelUploads);
        Assert.IsTrue(channel.ResumeUploads);
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(Path.GetTempPath()), ChannelSettings.DefaultClientSecretsFile),
            channel.ClientSecretsFile);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(Path.GetFullPath(Path.GetTempPath()), ChannelSettings.DefaultTokenCacheDirectory)),
            channel.TokenCacheDirectory);
    }

    [TestMethod]
    public void A_global_path_is_resolved_against_the_global_folder()
    {
        var global = new ChannelConfig { ClientSecretsFile = "secrets/client.json", TokenCacheDirectory = "auth" };

        var channel = ChannelSettings.Resolve(
            null,
            @"C:\work",
            global,
            @"C:\Users\someone\.utu");

        Assert.AreEqual(@"C:\Users\someone\.utu\secrets\client.json", channel.ClientSecretsFile);
        Assert.AreEqual(@"C:\Users\someone\.utu\auth", channel.TokenCacheDirectory);
    }

    [TestMethod]
    public void A_folder_path_is_resolved_against_the_working_folder_and_beats_the_global_path()
    {
        var global = new ChannelConfig { ClientSecretsFile = "global.json" };
        var folder = new ChannelConfig { ClientSecretsFile = "local.json" };

        var channel = ChannelSettings.Resolve(
            folder,
            @"C:\work",
            global,
            @"C:\Users\someone\.utu");

        Assert.AreEqual(@"C:\work\local.json", channel.ClientSecretsFile);
    }

    [TestMethod]
    public void The_state_file_defaults_next_to_the_working_folder()
    {
        var channel = ChannelSettings.Resolve(null, @"C:\work");

        Assert.AreEqual(@"C:\work\.utu\state.json", channel.StateFile);
    }

    [TestMethod]
    public void An_absolute_path_is_kept()
    {
        var folder = new ChannelConfig { StateFile = @"D:\state\utu.json" };

        var channel = ChannelSettings.Resolve(folder, @"C:\work");

        Assert.AreEqual(@"D:\state\utu.json", channel.StateFile);
    }

    [TestMethod]
    public void A_blank_value_is_treated_as_unset()
    {
        var folder = new ChannelConfig { ClientSecretsFile = " ", User = "", TokenCacheDirectory = "" };
        var global = new ChannelConfig { ClientSecretsFile = "global.json", User = "owner" };

        var channel = ChannelSettings.Resolve(folder, @"C:\work", global, @"C:\Users\someone\.utu");

        Assert.AreEqual(@"C:\Users\someone\.utu\global.json", channel.ClientSecretsFile);
        Assert.AreEqual("owner", channel.User);
        Assert.AreEqual(@"C:\work\.utu\auth", channel.TokenCacheDirectory);
    }
}

[TestClass]
public sealed class ConfigurationLoaderTests
{
    [TestMethod]
    public void The_global_configuration_is_merged_into_the_folder_configuration()
    {
        using var folder = new TempFolder();
        var globalFolder = folder.CreateDirectory(".utu");

        File.WriteAllText(
            Path.Combine(globalFolder, ConfigIO.DefaultFileName),
            """{ "channel": { "name": "Global", "parallelUploads": 5 }, "defaults": { "license": "creativeCommon" } }""");

        File.WriteAllText(
            folder[ConfigIO.DefaultFileName],
            """{ "defaults": { "category": "Music" }, "videos": [ { "file": "a.mp4" } ] }""");

        var loaded = ConfigurationLoader.Load(folder.Path, homeFolder: folder.Path);

        Assert.AreEqual(folder[ConfigIO.DefaultFileName], loaded.ConfigPath);
        Assert.AreEqual(Path.Combine(globalFolder, ConfigIO.DefaultFileName), loaded.GlobalPath);
        Assert.IsTrue(loaded.Resolved.HasGlobal);
        Assert.AreEqual("Global", loaded.Resolved.Channel.Name);
        Assert.AreEqual(5, loaded.Resolved.Channel.ParallelUploads);
        Assert.AreEqual("Music", loaded.Resolved.Defaults.Category);
        Assert.AreEqual(VideoLicense.CreativeCommon, loaded.Resolved.Defaults.License);
    }

    [TestMethod]
    public void An_explicitly_named_configuration_ignores_the_global_configuration()
    {
        using var folder = new TempFolder();
        var globalFolder = folder.CreateDirectory(".utu");
        File.WriteAllText(
            Path.Combine(globalFolder, ConfigIO.DefaultFileName),
            """{ "channel": { "name": "Global" } }""");
        folder.WriteFile("other.json", "{}");

        var loaded = ConfigurationLoader.Load(folder.Path, "other.json", homeFolder: folder.Path);

        Assert.IsNull(loaded.GlobalPath);
        Assert.IsFalse(loaded.Resolved.HasGlobal);
        Assert.IsNull(loaded.Resolved.Channel.Name);
    }

    [TestMethod]
    public void A_missing_global_configuration_is_not_an_error()
    {
        using var folder = new TempFolder();
        folder.WriteFile(ConfigIO.DefaultFileName, "{}");

        var loaded = ConfigurationLoader.Load(folder.Path, homeFolder: folder.Path);

        Assert.IsNull(loaded.GlobalPath);
        Assert.IsNull(loaded.GlobalFolder);
        Assert.AreEqual(folder.Path, loaded.Resolved.WorkingFolder);
    }

    [TestMethod]
    public void A_missing_folder_configuration_is_an_error()
    {
        using var folder = new TempFolder();

        var exception = Assert.ThrowsExactly<UtuException>(() => ConfigurationLoader.Load(folder.Path, homeFolder: folder.Path));

        StringAssert.Contains(exception.Message, "utu init");
    }

    [TestMethod]
    public void A_missing_folder_configuration_is_allowed_for_init()
    {
        using var folder = new TempFolder();

        var loaded = ConfigurationLoader.Load(folder.Path, allowMissingFolderConfig: true, homeFolder: folder.Path);

        Assert.AreEqual(0, loaded.Resolved.Videos.Count);
    }

    [TestMethod]
    public void The_global_configuration_is_not_merged_into_itself()
    {
        using var folder = new TempFolder();
        var globalFolder = folder.CreateDirectory(".utu");
        var globalPath = Path.Combine(globalFolder, ConfigIO.DefaultFileName);
        File.WriteAllText(globalPath, """{ "channel": { "name": "Global" } }""");

        var loaderHome = folder.Path;
        var loaded = ConfigurationLoader.Load(
            globalFolder,
            "utu.json",
            homeFolder: loaderHome);

        Assert.IsNull(loaded.GlobalPath);
        Assert.IsNull(ConfigurationLoader.FindGlobalConfig(globalPath, null, loaderHome));
    }

    [TestMethod]
    public void The_home_variable_can_relocate_the_global_configuration()
    {
        using var folder = new TempFolder();

        Assert.AreEqual(
            Path.Combine(folder.Path, ConfigIO.DefaultFileName),
            ConfigurationLoader.GlobalConfigPath(folder.Path, folder.Path));
    }
}
