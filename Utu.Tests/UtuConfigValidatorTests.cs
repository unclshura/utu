using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class UtuConfigValidatorTests
{
    [TestMethod]
    public void A_valid_configuration_has_no_problems()
    {
        var result = UtuConfigValidator.Validate(Valid());

        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0, result.Issues.Count);
    }

    [TestMethod]
    public void Parallel_uploads_must_be_at_least_one()
    {
        var config = Valid();
            config.Channel!.ParallelUploads = 0;

        var result = UtuConfigValidator.Validate(config);

        Assert.IsTrue(result.HasErrors);
        Assert.AreEqual("channel.parallelUploads", result.Errors.Single().Setting);
    }

    [TestMethod]
        public void A_blank_channel_value_is_inherited_rather_than_rejected()
    {
        var config = Valid();
            config.Channel!.ClientSecretsFile = " ";

            var result = UtuConfigValidator.Validate(config, Path.GetTempPath());

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(
                Path.Combine(Path.GetFullPath(Path.GetTempPath()), ChannelSettings.DefaultClientSecretsFile),
                ResolvedConfiguration.Resolve(Path.GetTempPath(), config).Channel.ClientSecretsFile);
    }

    [TestMethod]
    public void A_configuration_without_videos_is_a_warning()
    {
        var config = Valid();
        config.Videos = [];

        var result = UtuConfigValidator.Validate(config);

        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("videos", result.Warnings.Single().Setting);
        StringAssert.Contains(result.Warnings.Single().Message, "utu init");
    }

    [TestMethod]
    public void A_duplicate_video_is_an_error()
    {
        var config = Valid();
        config.Videos = [TestConfig.Entry("a.mp4"), TestConfig.Entry("./A.MP4")];

        var result = UtuConfigValidator.Validate(config);

        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains(result.Errors.Single().Message, "2 times");
    }

    [TestMethod]
    public void A_missing_channel_name_is_a_warning()
    {
        var config = Valid();
            config.Channel!.Name = null;

        var result = UtuConfigValidator.Validate(config);

        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("channel.name", result.Warnings.Single().Setting);
    }

    private static UtuConfig Valid()
    {
        var config = TestConfig.With(TestConfig.Entry("a.mp4"));
        config.Channel!.ClientSecretsFile = "client_secret.json";
        return config;
    }
}
