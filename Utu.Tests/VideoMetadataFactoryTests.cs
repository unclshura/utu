using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class VideoMetadataFactoryTests
{
    [TestMethod]
    public void Creates_the_request_body_from_the_settings()
    {
        var settings = new VideoSettings
        {
            Title = "  A song  ",
            Description = "Description",
            Tags = ["rock", " rock ", "roll"],
            Category = "Gaming",
            Privacy = Privacy.Unlisted,
            License = VideoLicense.CreativeCommon,
            Embeddable = false,
            ShowLikeCount = false,
            MadeForKids = true,
            AiUse = true,
        };

        var body = VideoMetadataFactory.CreateBody(settings);

        Assert.AreEqual("A song", body.Snippet.Title);
        Assert.AreEqual("Description", body.Snippet.Description);
        Assert.AreEqual("20", body.Snippet.CategoryId);
        CollectionAssert.AreEqual(new[] { "rock", "roll" }, body.Snippet.Tags.ToArray());
        Assert.AreEqual("unlisted", body.Status.PrivacyStatus);
        Assert.AreEqual("creativeCommon", body.Status.License);
        Assert.IsFalse(body.Status.Embeddable);
        Assert.IsFalse(body.Status.PublicStatsViewable);
        Assert.IsTrue(body.Status.SelfDeclaredMadeForKids);
        Assert.IsTrue(body.Status.ContainsSyntheticMedia);
    }

    [TestMethod]
    public void Defaults_the_category_to_music_and_the_description_to_empty()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T" });

        Assert.AreEqual(CategoryRegistry.Music, body.Snippet.CategoryId);
        Assert.AreEqual(string.Empty, body.Snippet.Description);
        Assert.IsNull(body.Snippet.Tags);
    }

    [TestMethod]
    public void Maps_privacy_and_licence()
    {
        Assert.AreEqual("public", VideoMetadataFactory.ToApiPrivacy(Privacy.Public));
        Assert.AreEqual("unlisted", VideoMetadataFactory.ToApiPrivacy(Privacy.Unlisted));
        Assert.AreEqual("private", VideoMetadataFactory.ToApiPrivacy(Privacy.Private));
        Assert.AreEqual("private", VideoMetadataFactory.ToApiPrivacy(null));

        Assert.AreEqual("youtube", VideoMetadataFactory.ToApiLicense(VideoLicense.Youtube));
        Assert.AreEqual("creativeCommon", VideoMetadataFactory.ToApiLicense(VideoLicense.CreativeCommon));
        Assert.IsNull(VideoMetadataFactory.ToApiLicense(null));
    }

    [TestMethod]
    public void Converts_the_publishing_time_to_utc()
    {
        var offset = TimeSpan.FromHours(2);
        var settings = new VideoSettings
        {
            Title = "T",
            PublishAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, offset),
        };

        var body = VideoMetadataFactory.CreateBody(settings);

        Assert.AreEqual(settings.PublishAt!.Value.ToUniversalTime(), body.Status.PublishAtDateTimeOffset);
        Assert.AreEqual(TimeSpan.Zero, body.Status.PublishAtDateTimeOffset!.Value.Offset);
    }

    [TestMethod]
    public void Creates_recording_details_only_when_configured()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T" });
        Assert.IsNull(body.RecordingDetails);

        var recorded = VideoMetadataFactory.CreateBody(new VideoSettings
        {
            Title = "T",
            RecordingDate = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero),
            Location = "48.8584,2.2945,35",
            LocationDescription = " Paris ",
        });

        Assert.AreEqual(new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero), recorded.RecordingDetails.RecordingDateDateTimeOffset);
        Assert.AreEqual(48.8584, recorded.RecordingDetails.Location.Latitude!.Value, 0.0001);
        Assert.AreEqual(35, recorded.RecordingDetails.Location.Altitude!.Value, 0.0001);
        Assert.AreEqual("Paris", recorded.RecordingDetails.LocationDescription);
    }

    [TestMethod]
    public void Ignores_an_invalid_location()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings
        {
            Title = "T",
            RecordingDate = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero),
            Location = "not a location",
        });

        Assert.IsNull(body.RecordingDetails.Location);
    }

    [TestMethod]
    public void Builds_the_part_parameter_from_the_configured_values()
    {
        Assert.AreEqual("snippet,status", VideoMetadataFactory.BuildParts(new VideoSettings { Title = "T" }));

        var full = new VideoSettings
        {
            Title = "T",
            RecordingDate = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero),
            Localizations = new Dictionary<string, VideoLocalization> { ["fr"] = new VideoLocalization { Title = "Titre" } },
            PaidPromotion = true,
            BrandPartnership = "@partner",
        };

        Assert.AreEqual(
            "snippet,status,recordingDetails,localizations,paidProductPlacementDetails,brandPartner",
            VideoMetadataFactory.BuildParts(full));
    }

    [TestMethod]
    public void Creates_brand_partner_from_a_channel_id_or_a_handle()
    {
        var handle = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T", BrandPartnership = " @partner " });
        Assert.AreEqual("@partner", handle.BrandPartner.ChannelHandle);
        Assert.IsNull(handle.BrandPartner.ChannelId);

        var channelId = "UC" + new string('x', 22);
        var byId = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T", BrandPartnership = channelId });
        Assert.AreEqual(channelId, byId.BrandPartner.ChannelId);
        Assert.IsNull(byId.BrandPartner.ChannelHandle);
    }

    [TestMethod]
    public void Reports_a_paid_promotion()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T", PaidPromotion = true });

        Assert.IsTrue(body.PaidProductPlacementDetails.HasPaidProductPlacement);
    }

    [TestMethod]
    public void Keeps_only_usable_localizations()
    {
        var settings = new VideoSettings
        {
            Title = "T",
            Language = "en",
            Localizations =
            new Dictionary<string, VideoLocalization> {
                ["fr"] = new VideoLocalization { Title = "Titre", Description = "Description" },
                ["nonsense!"] = new VideoLocalization { Title = "Nope" },
                ["de"] = new VideoLocalization(),
            },
        };

        var body = VideoMetadataFactory.CreateBody(settings);

        Assert.AreEqual(1, body.Localizations.Count);
        Assert.AreEqual("Titre", body.Localizations["fr"].Title);
    }

    [TestMethod]
    public void Normalizes_the_language_codes()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T", Language = "EN-us" });

        Assert.AreEqual("en-US", body.Snippet.DefaultLanguage);
        Assert.AreEqual("en-US", body.Snippet.DefaultAudioLanguage);
    }

    [TestMethod]
    public void Prefers_the_audio_language_when_it_is_set()
    {
        var body = VideoMetadataFactory.CreateBody(new VideoSettings { Title = "T", Language = "en", AudioLanguage = "FR" });

        Assert.AreEqual("en", body.Snippet.DefaultLanguage);
        Assert.AreEqual("fr", body.Snippet.DefaultAudioLanguage);
    }

    [TestMethod]
    public void Studio_only_settings_are_reported_as_manual_work()
    {
        var quiet = new VideoSettings
        {
            Title = "T",
            Comments = CommentPolicy.On,
            Moderation = CommentModeration.Basic,
            WhoCanComment = CommentAudience.Everyone,
            ShortsRemixing = Configuration.ShortsRemixing.VideoAndAudio,
            AllowAutomaticPlaces = true,
        };

        Assert.AreEqual(0, VideoMetadataFactory.DescribeManualFollowUps(quiet).Count);

        var configured = new VideoSettings
        {
            Title = "T",
            Comments = CommentPolicy.Off,
            Moderation = CommentModeration.Strict,
            WhoCanComment = CommentAudience.None,
            ShortsRemixing = Configuration.ShortsRemixing.None,
            AllowAutomaticPlaces = false,
            Collaboration = ["@other"],
            FeaturedPlaces = ["Paris"],
        };

        var followUps = VideoMetadataFactory.DescribeManualFollowUps(configured);

        Assert.AreEqual(7, followUps.Count);
        StringAssert.Contains(string.Join('\n', followUps), "Comments: off");
        StringAssert.Contains(string.Join('\n', followUps), "Moderation: strict");
        StringAssert.Contains(string.Join('\n', followUps), "Who can comment: none");
        StringAssert.Contains(string.Join('\n', followUps), "Shorts remixing: don't allow");
        StringAssert.Contains(string.Join('\n', followUps), "Allow automatic places: no");
        StringAssert.Contains(string.Join('\n', followUps), "Collaboration: invite @other");
        StringAssert.Contains(string.Join('\n', followUps), "Featured places: Paris");
    }
}
