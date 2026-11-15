using Google.Apis.YouTube.v3.Data;
using Utu.Configuration;
using GoogleLocalization = Google.Apis.YouTube.v3.Data.VideoLocalization;

namespace Utu.Core;

/// <summary>Builds the YouTube API request body from a video configuration.</summary>
public static class VideoMetadataFactory
{
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 5000;
    public const int MaxTagsLength = 500;

    /// <summary>Creates the <c>videos.insert</c> body for a video.</summary>
    public static Video CreateBody(VideoSettings settings)
    {
        var localizations = CreateLocalizations(settings);

        return new Video
        {
            Snippet = new VideoSnippet
            {
                Title = settings.Title?.Trim(),
                Description = settings.Description ?? string.Empty,
                CategoryId = CategoryRegistry.ResolveOrDefault(settings.Category),
                Tags = CreateTags(settings),
                DefaultLanguage = LanguageCodes.Normalize(settings.Language),
                DefaultAudioLanguage = LanguageCodes.Normalize(settings.AudioLanguage ?? settings.Language),
            },
            Status = new VideoStatus
            {
                PrivacyStatus = ToApiPrivacy(settings.Privacy),
                Embeddable = settings.Embeddable,
                License = ToApiLicense(settings.License),
                PublicStatsViewable = settings.ShowLikeCount,
                SelfDeclaredMadeForKids = settings.MadeForKids,
                ContainsSyntheticMedia = settings.AiUse,
                PublishAtDateTimeOffset = settings.PublishAt?.ToUniversalTime(),
            },
            RecordingDetails = CreateRecordingDetails(settings),
            Localizations = localizations,
            PaidProductPlacementDetails = settings.PaidPromotion is { } paidPromotion
                ? new VideoPaidProductPlacementDetails { HasPaidProductPlacement = paidPromotion }
                : null,
            BrandPartner = CreateBrandPartner(settings.BrandPartnership),
        };
    }

    /// <summary>
    /// Returns the <c>part</c> parameter for <c>videos.insert</c>. Optional
    /// parts are only requested when they carry a value, which keeps the request
    /// minimal.
    /// </summary>
    public static string BuildParts(VideoSettings settings)
    {
        var parts = new List<string> { "snippet", "status" };

        if (CreateRecordingDetails(settings) is not null)
        {
            parts.Add("recordingDetails");
        }

        if (CreateLocalizations(settings) is not null)
        {
            parts.Add("localizations");
        }

        if (settings.PaidPromotion is not null)
        {
            parts.Add("paidProductPlacementDetails");
        }

        if (CreateBrandPartner(settings.BrandPartnership) is not null)
        {
            parts.Add("brandPartner");
        }

        return string.Join(',', parts);
    }

    /// <summary>
    /// Lists the configured settings that the YouTube Data API cannot write, but
    /// only when they differ from what YouTube Studio already applies on its own.
    /// They are reported to the user as manual steps in YouTube Studio.
    /// </summary>
    public static IReadOnlyList<string> DescribeManualFollowUps(VideoSettings settings)
    {
        var followUps = new List<string>();

        if (settings.Collaboration is { Count: > 0 })
        {
            followUps.Add($"Collaboration: invite {string.Join(", ", settings.Collaboration)}");
        }

        if (settings.FeaturedPlaces is { Count: > 0 })
        {
            followUps.Add($"Featured places: {string.Join(", ", settings.FeaturedPlaces)}");
        }

        if (settings.AllowAutomaticPlaces == false)
        {
            followUps.Add("Allow automatic places: no");
        }

        if (settings.ShortsRemixing is { } shortsRemixing && shortsRemixing != Configuration.ShortsRemixing.VideoAndAudio)
        {
            followUps.Add($"Shorts remixing: {DescribeShortsRemixing(shortsRemixing)}");
        }

        if (settings.Comments == CommentPolicy.Off)
        {
            followUps.Add("Comments: off");
        }

        if (settings.Moderation is { } moderation && moderation != CommentModeration.Basic)
        {
            followUps.Add($"Moderation: {DescribeModeration(moderation)}");
        }

        if (settings.WhoCanComment is { } whoCanComment && whoCanComment != CommentAudience.Everyone)
        {
            followUps.Add($"Who can comment: {DescribeCommentAudience(whoCanComment)}");
        }

        return followUps;
    }

    /// <summary>Maps a privacy setting to the API value.</summary>
    public static string ToApiPrivacy(Privacy? privacy) => privacy switch
    {
        Privacy.Public => "public",
        Privacy.Unlisted => "unlisted",
        _ => "private",
    };

    /// <summary>Maps a licence setting to the API value.</summary>
    public static string? ToApiLicense(VideoLicense? license) => license switch
    {
        VideoLicense.Youtube => "youtube",
        VideoLicense.CreativeCommon => "creativeCommon",
        _ => null,
    };

    private static IList<string>? CreateTags(VideoSettings settings)
    {
        if (settings.Tags is not { Count: > 0 })
        {
            return null;
        }

        var tags = settings.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tags.Count == 0 ? null : tags;
    }

    private static VideoRecordingDetails? CreateRecordingDetails(VideoSettings settings)
    {
        var hasLocation = GeoPointParser.TryParse(settings.Location, out var latitude, out var longitude, out var altitude);
        var hasDescription = !string.IsNullOrWhiteSpace(settings.LocationDescription);

        if (!hasLocation && !hasDescription && settings.RecordingDate is null)
        {
            return null;
        }

        return new VideoRecordingDetails
        {
            RecordingDateDateTimeOffset = settings.RecordingDate?.ToUniversalTime(),
            Location = hasLocation
                ? new GeoPoint { Latitude = latitude, Longitude = longitude, Altitude = altitude }
                : null,
            LocationDescription = hasDescription ? settings.LocationDescription!.Trim() : null,
        };
    }

    private static BrandPartner? CreateBrandPartner(string? brandPartnership)
    {
        if (string.IsNullOrWhiteSpace(brandPartnership))
        {
            return null;
        }

        var value = brandPartnership.Trim();
        return value.StartsWith("UC", StringComparison.Ordinal) && value.Length == 24
            ? new BrandPartner { ChannelId = value }
            : new BrandPartner { ChannelHandle = value };
    }

    private static Dictionary<string, GoogleLocalization>? CreateLocalizations(VideoSettings settings)
    {
        if (settings.Localizations is not { Count: > 0 })
        {
            return null;
        }

        var localizations = new Dictionary<string, GoogleLocalization>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, localization) in settings.Localizations)
        {
            var key = LanguageCodes.Normalize(language);
            if (key is null || (localization.Title is null && localization.Description is null))
            {
                continue;
            }

            localizations[key] = new GoogleLocalization
            {
                Title = localization.Title,
                Description = localization.Description,
            };
        }

        return localizations.Count == 0 ? null : localizations;
    }

    private static string DescribeShortsRemixing(Configuration.ShortsRemixing value) => value switch
    {
        Configuration.ShortsRemixing.VideoAndAudio => "allow video and audio",
        Configuration.ShortsRemixing.AudioOnly => "allow audio only",
        _ => "don't allow",
    };

    private static string DescribeModeration(CommentModeration value) => value switch
    {
        CommentModeration.Basic => "basic",
        CommentModeration.Strict => "strict",
        CommentModeration.HoldAll => "hold all for review",
        _ => "hold potentially inappropriate for review",
    };

    private static string DescribeCommentAudience(CommentAudience value) => value switch
    {
        CommentAudience.Everyone => "everyone",
        CommentAudience.Subscribers => "subscribers",
        _ => "none",
    };
}
