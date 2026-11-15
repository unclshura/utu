using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class VideoSettingsValidatorTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_complete_configuration_has_no_problems()
    {
        var settings = new VideoSettings
        {
            Title = "A song",
            Description = "Description",
            Tags = ["rock"],
            Category = "Music",
            Language = "en",
            RecordingDate = Now.AddYears(-1),
            Location = "48.8584,2.2945",
            PublishAt = Now.AddDays(1),
            Privacy = Privacy.Private,
        };

        var result = VideoSettingsValidator.Validate(settings, thumbnailPath: null, now: Now);

        Assert.AreEqual(0, result.Issues.Count);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void A_title_is_required()
    {
        var result = VideoSettingsValidator.Validate(new VideoSettings(), thumbnailPath: null, now: Now);

        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains(result.Issues[0].Setting, "title");
    }

    [TestMethod]
    public void A_title_may_not_be_too_long_or_contain_angle_brackets()
    {
        var tooLong = VideoSettingsValidator.Validate(new VideoSettings { Title = new string('a', 101) }, now: Now);
        Assert.IsTrue(tooLong.Errors.Any(issue => issue.Setting == "title"));

        var brackets = VideoSettingsValidator.Validate(new VideoSettings { Title = "a < b" }, now: Now);
        Assert.IsTrue(brackets.Errors.Any(issue => issue.Setting == "title"));
    }

    [TestMethod]
    public void Angle_brackets_in_the_description_are_a_warning()
    {
        var result = VideoSettingsValidator.Validate(new VideoSettings { Title = "T", Description = "<b>" }, now: Now);

        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Warnings.Count(warning => warning.Setting == "description"));
    }

    [TestMethod]
    public void Tags_may_not_exceed_the_character_limit()
    {
        var settings = new VideoSettings { Title = "T", Tags = [.. Enumerable.Repeat(new string('a', 100), 6)] };

        var result = VideoSettingsValidator.Validate(settings, now: Now);

        Assert.IsTrue(result.Errors.Any(issue => issue.Setting == "tags"));
    }

    [TestMethod]
    public void Tags_within_the_limit_are_accepted()
    {
        var settings = new VideoSettings { Title = "T", Tags = [.. Enumerable.Repeat(new string('a', 100), 4)] };

        var result = VideoSettingsValidator.Validate(settings, now: Now);

        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void An_unknown_category_is_an_error()
    {
        var result = VideoSettingsValidator.Validate(new VideoSettings { Title = "T", Category = "Nope" }, now: Now);

        Assert.IsTrue(result.Errors.Any(issue => issue.Setting == "category"));
    }

    [TestMethod]
    public void A_scheduled_publication_must_be_in_the_future_and_private()
    {
        var past = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", PublishAt = Now.AddHours(-1) },
            now: Now);
        Assert.IsTrue(past.Errors.Any(issue => issue.Setting == "publishAt"));

        var notPrivate = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", PublishAt = Now.AddDays(1), Privacy = Privacy.Public },
            now: Now);
        Assert.IsTrue(notPrivate.Errors.Any(issue => issue.Setting == "publishAt"));

        var privateSchedule = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", PublishAt = Now.AddDays(1), Privacy = Privacy.Private },
            now: Now);
        Assert.IsFalse(privateSchedule.HasErrors);
    }

    [TestMethod]
    public void A_publication_that_is_too_soon_is_a_warning()
    {
        var result = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", PublishAt = Now.AddMinutes(5), Privacy = Privacy.Private },
            now: Now);

        Assert.IsFalse(result.HasErrors);
        Assert.IsTrue(result.Warnings.Any(issue => issue.Setting == "publishAt"));
    }

    [TestMethod]
    public void Recording_dates_are_checked()
    {
        var future = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", RecordingDate = Now.AddDays(1) },
            now: Now);
        Assert.IsTrue(future.Errors.Any(issue => issue.Setting == "recordingDate"));

        var ancient = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", RecordingDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            now: Now);
        Assert.IsFalse(ancient.HasErrors);
        Assert.IsTrue(ancient.Warnings.Any(issue => issue.Setting == "recordingDate"));
    }

    [TestMethod]
    public void Locations_are_checked()
    {
        var invalid = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", RecordingDate = Now.AddDays(-1), Location = "home" },
            now: Now);
        Assert.IsTrue(invalid.Errors.Any(issue => issue.Setting == "location"));

        var withoutDate = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", Location = "48.8584,2.2945" },
            now: Now);
        Assert.IsFalse(withoutDate.HasErrors);
        Assert.IsTrue(withoutDate.Warnings.Any(issue => issue.Setting == "location"));
    }

    [TestMethod]
    public void Language_codes_are_checked()
    {
        var language = VideoSettingsValidator.Validate(new VideoSettings { Title = "T", Language = "e" }, now: Now);
        Assert.IsTrue(language.Errors.Any(issue => issue.Setting == "language"));

        var audio = VideoSettingsValidator.Validate(new VideoSettings { Title = "T", AudioLanguage = "en_US" }, now: Now);
        Assert.IsTrue(audio.Errors.Any(issue => issue.Setting == "audioLanguage"));
    }

    [TestMethod]
    public void Localizations_need_a_language_and_content()
    {
        var withoutLanguage = VideoSettingsValidator.Validate(
            new VideoSettings { Title = "T", Localizations = new Dictionary<string, VideoLocalization> { ["fr"] = new VideoLocalization { Title = "Titre" } } },
            now: Now);
        Assert.IsTrue(withoutLanguage.Errors.Any(issue => issue.Setting == "localizations"));

        var empty = VideoSettingsValidator.Validate(
            new VideoSettings
            {
                Title = "T",
                Language = "en",
                Localizations = new Dictionary<string, VideoLocalization> { ["fr"] = new VideoLocalization() },
            },
            now: Now);
        Assert.IsTrue(empty.Errors.Any(issue => issue.Setting == "localizations"));

        var invalidCode = VideoSettingsValidator.Validate(
            new VideoSettings
            {
                Title = "T",
                Language = "en",
                Localizations = new Dictionary<string, VideoLocalization> { ["nonsense!"] = new VideoLocalization { Title = "Titre" } },
            },
            now: Now);
        Assert.IsTrue(invalidCode.Errors.Any(issue => issue.Setting == "localizations"));
    }

    [TestMethod]
    public void A_localization_of_the_default_language_is_a_warning()
    {
        var result = VideoSettingsValidator.Validate(
            new VideoSettings
            {
                Title = "T",
                Language = "en",
                Localizations = new Dictionary<string, VideoLocalization> { ["EN"] = new VideoLocalization { Title = "Title" } },
            },
            now: Now);

        Assert.IsFalse(result.HasErrors);
        Assert.IsTrue(result.Warnings.Any(issue => issue.Setting == "localizations"));
    }

    [TestMethod]
    public void A_missing_thumbnail_is_a_warning()
    {
        using var folder = new TempFolder();
        var missing = folder["song.jpg"];

        var result = VideoSettingsValidator.Validate(new VideoSettings { Title = "T" }, missing, Now);

        Assert.IsFalse(result.HasErrors);
        Assert.IsTrue(result.Warnings.Any(issue => issue.Setting == "thumbnail"));
    }

    [TestMethod]
    public void An_existing_thumbnail_is_accepted()
    {
        using var folder = new TempFolder();
        var thumbnail = folder.WriteFile("song.jpg");

        var result = VideoSettingsValidator.Validate(new VideoSettings { Title = "T" }, thumbnail, Now);

        Assert.AreEqual(0, result.Issues.Count);
    }

    [TestMethod]
    public void Issues_describe_the_setting_that_caused_them()
    {
        var result = VideoSettingsValidator.Validate(new VideoSettings(), now: Now);

        StringAssert.Contains(result.Issues.Single().ToString(), "title");
    }
}
