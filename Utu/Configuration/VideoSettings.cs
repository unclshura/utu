using System.Text.Json.Serialization;

namespace Utu.Configuration;

/// <summary>
/// Upload parameters of a single video. Every property is optional: a video
/// entry only carries the values that differ from <c>defaults</c>.
/// </summary>
public class VideoSettings
{
    /// <summary>Video title. Required before uploading, at most 100 characters.</summary>
    public string? Title { get; set; }

    /// <summary>Video description, at most 5000 characters.</summary>
    public string? Description { get; set; }

    /// <summary>Keywords. YouTube allows at most 500 characters in total.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Thumbnail image, relative to the working folder.</summary>
    public string? Thumbnail { get; set; }

    /// <summary>Playlists the video is added to, by name or by id.</summary>
    public List<string>? Playlists { get; set; }

    /// <summary>Category name ("Music") or numeric id ("10").</summary>
    public string? Category { get; set; }

    /// <summary>Whether the video is made for kids. utu defaults to <see langword="false" />.</summary>
    public bool? MadeForKids { get; set; }

    /// <summary>Initial visibility. utu defaults to <see cref="Configuration.Privacy.Private" />.</summary>
    public Privacy? Privacy { get; set; }

    /// <summary>Date and place the video was recorded (ISO 8601).</summary>
    public DateTimeOffset? RecordingDate { get; set; }

    /// <summary>Gelocation of the recording as <c>latitude,longitude[,altitude]</c>.</summary>
    public string? Location { get; set; }

    /// <summary>Human readable description of the recording location.</summary>
    public string? LocationDescription { get; set; }

    /// <summary>Publication time (ISO 8601, in the future). Requires private visibility.</summary>
    public DateTimeOffset? PublishAt { get; set; }

    /// <summary>Whether the video contains paid promotion.</summary>
    public bool? PaidPromotion { get; set; }

    /// <summary>Channel id (<c>UC...</c>) or handle of a brand partner.</summary>
    public string? BrandPartnership { get; set; }

    /// <summary>Whether the video contains altered or synthetic media.</summary>
    public bool? AiUse { get; set; }

    /// <summary>Default language of the video, for example <c>en-US</c>.</summary>
    public string? Language { get; set; }

    /// <summary>Language of the audio track, when it differs from <see cref="Language" />.</summary>
    public string? AudioLanguage { get; set; }

    /// <summary>Localized titles and descriptions keyed by language code.</summary>
    public Dictionary<string, VideoLocalization>? Localizations { get; set; }

    /// <summary>Licence. utu defaults to the standard YouTube licence.</summary>
    public VideoLicense? License { get; set; }

    /// <summary>Whether the video may be embedded. utu defaults to <see langword="true" />.</summary>
    public bool? Embeddable { get; set; }

    /// <summary>Whether the like count is shown. utu defaults to <see langword="true" />.</summary>
    public bool? ShowLikeCount { get; set; }

    /// <summary>Shorts remix permission. YouTube Studio only.</summary>
    public ShortsRemixing? ShortsRemixing { get; set; }

    /// <summary>Whether comments are enabled. YouTube Studio only.</summary>
    public CommentPolicy? Comments { get; set; }

    /// <summary>Comment moderation level. YouTube Studio only.</summary>
    public CommentModeration? Moderation { get; set; }

    /// <summary>Who is allowed to comment. YouTube Studio only.</summary>
    public CommentAudience? WhoCanComment { get; set; }

    /// <summary>Places featured in the video. YouTube Studio only.</summary>
    public List<string>? FeaturedPlaces { get; set; }

    /// <summary>Whether YouTube may add places automatically. YouTube Studio only.</summary>
    public bool? AllowAutomaticPlaces { get; set; }

    /// <summary>Channels invited to collaborate. YouTube Studio only.</summary>
    public List<string>? Collaboration { get; set; }

    /// <summary>Returns a deep copy of these settings.</summary>
    public VideoSettings Clone() => WithFallback(null);

    /// <summary>
    /// Returns a deep copy in which unset values are taken from
    /// <paramref name="fallback" />. Lists and dictionaries are replaced as a
    /// whole rather than merged.
    /// </summary>
    public VideoSettings WithFallback(VideoSettings? fallback)
    {
        var source = this;

        return new VideoSettings
        {
            Title = source.Title ?? fallback?.Title,
            Description = source.Description ?? fallback?.Description,
            Tags = CopyList(source.Tags, fallback?.Tags),
            Thumbnail = source.Thumbnail ?? fallback?.Thumbnail,
            Playlists = CopyList(source.Playlists, fallback?.Playlists),
            Category = source.Category ?? fallback?.Category,
            MadeForKids = source.MadeForKids ?? fallback?.MadeForKids,
            Privacy = source.Privacy ?? fallback?.Privacy,
            RecordingDate = source.RecordingDate ?? fallback?.RecordingDate,
            Location = source.Location ?? fallback?.Location,
            LocationDescription = source.LocationDescription ?? fallback?.LocationDescription,
            PublishAt = source.PublishAt ?? fallback?.PublishAt,
            PaidPromotion = source.PaidPromotion ?? fallback?.PaidPromotion,
            BrandPartnership = source.BrandPartnership ?? fallback?.BrandPartnership,
            AiUse = source.AiUse ?? fallback?.AiUse,
            Language = source.Language ?? fallback?.Language,
            AudioLanguage = source.AudioLanguage ?? fallback?.AudioLanguage,
            Localizations = CopyLocalizations(source.Localizations, fallback?.Localizations),
            License = source.License ?? fallback?.License,
            Embeddable = source.Embeddable ?? fallback?.Embeddable,
            ShowLikeCount = source.ShowLikeCount ?? fallback?.ShowLikeCount,
            ShortsRemixing = source.ShortsRemixing ?? fallback?.ShortsRemixing,
            Comments = source.Comments ?? fallback?.Comments,
            Moderation = source.Moderation ?? fallback?.Moderation,
            WhoCanComment = source.WhoCanComment ?? fallback?.WhoCanComment,
            FeaturedPlaces = CopyList(source.FeaturedPlaces, fallback?.FeaturedPlaces),
            AllowAutomaticPlaces = source.AllowAutomaticPlaces ?? fallback?.AllowAutomaticPlaces,
            Collaboration = CopyList(source.Collaboration, fallback?.Collaboration),
        };
    }

    private static List<string>? CopyList(List<string>? source, List<string>? fallback) =>
        source is not null ? [.. source] : fallback is null ? null : [.. fallback];

    private static Dictionary<string, VideoLocalization>? CopyLocalizations(
        Dictionary<string, VideoLocalization>? source,
        Dictionary<string, VideoLocalization>? fallback)
    {
        var from = source ?? fallback;
        if (from is null)
        {
            return null;
        }

        var copy = new Dictionary<string, VideoLocalization>(from.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (language, localization) in from)
        {
            copy[language] = new VideoLocalization
            {
                Title = localization?.Title,
                Description = localization?.Description,
            };
        }

        return copy;
    }
}

/// <summary>Localized title and description of a video.</summary>
public sealed class VideoLocalization
{
    /// <summary>Localized title.</summary>
    public string? Title { get; set; }

    /// <summary>Localized description.</summary>
    public string? Description { get; set; }
}

/// <summary>One entry of the <c>videos</c> array.</summary>
public sealed class VideoEntry : VideoSettings
{
    /// <summary>Video file, relative to the working folder.</summary>
    public string? File { get; set; }

    /// <summary>
    /// Set to <see langword="false" /> to keep the entry in the configuration
    /// but out of the upload.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>Whether the video takes part in the upload; missing means yes.</summary>
    [JsonIgnore]
    public bool IsEnabled => Enabled ?? true;

    /// <summary>Returns these settings without the file specific properties.</summary>
    public VideoSettings ToSettings() => Clone();
}
