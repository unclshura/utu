using Utu.Configuration;

namespace Utu.Core;

/// <summary>
/// Checks a resolved video configuration against the limits of the YouTube Data
/// API, so that problems surface before a long upload starts.
/// </summary>
public static class VideoSettingsValidator
{
    /// <summary>YouTube rejects recording dates before the site launched.</summary>
    public static readonly DateTimeOffset FirstSupportedRecordingDate =
        new(2005, 2, 15, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Recommended minimum lead time for a scheduled publication.</summary>
    public static readonly TimeSpan MinimumSchedulingLeadTime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Validates <paramref name="settings" />.
    /// </summary>
    /// <param name="settings">Fully resolved settings of one video.</param>
    /// <param name="thumbnailPath">
    /// Absolute path of the configured thumbnail, or <see langword="null" /> when
    /// no thumbnail is configured.
    /// </param>
    /// <param name="now">Current time, for the date checks.</param>
    public static ValidationResult Validate(
        VideoSettings settings,
        string? thumbnailPath = null,
        DateTimeOffset? now = null)
    {
        var result = new ValidationResult();
        var moment = now ?? DateTimeOffset.UtcNow;

        ValidateTitle(settings, result);
        ValidateDescription(settings, result);
        ValidateTags(settings, result);
        ValidateCategory(settings, result);
        ValidateSchedule(settings, result, moment);
        ValidateRecording(settings, result, moment);
        ValidateLanguages(settings, result);
        ValidateLocalizations(settings, result);
        ValidateThumbnail(thumbnailPath, result);

        return result;
    }

    private static void ValidateTitle(VideoSettings settings, ValidationResult result)
    {
        var title = settings.Title?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            result.Error("title", "a title is required before a video can be uploaded");
            return;
        }

        if (title.Length > VideoMetadataFactory.MaxTitleLength)
        {
            result.Error(
                "title",
                $"the title is {title.Length} characters long, YouTube allows at most {VideoMetadataFactory.MaxTitleLength}");
        }

        if (title.Contains('<') || title.Contains('>'))
        {
            result.Error("title", "YouTube does not allow '<' or '>' in a title");
        }
    }

    private static void ValidateDescription(VideoSettings settings, ValidationResult result)
    {
        var description = settings.Description;
        if (string.IsNullOrEmpty(description))
        {
            return;
        }

        if (description.Length > VideoMetadataFactory.MaxDescriptionLength)
        {
            result.Error(
                "description",
                $"the description is {description.Length} characters long, YouTube allows at most {VideoMetadataFactory.MaxDescriptionLength}");
        }

        if (description.Contains('<') || description.Contains('>'))
        {
            result.Warn("description", "YouTube does not allow '<' or '>' in a description and rejects the upload");
        }
    }

    private static void ValidateTags(VideoSettings settings, ValidationResult result)
    {
        if (settings.Tags is not { Count: > 0 })
        {
            return;
        }

        var tags = settings.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToList();

        if (tags.Count == 0)
        {
            return;
        }

        var length = tags.Sum(tag => tag.Length) + tags.Count - 1;
        if (length > VideoMetadataFactory.MaxTagsLength)
        {
            result.Error(
                "tags",
                $"the tags use {length} characters, YouTube allows at most {VideoMetadataFactory.MaxTagsLength}");
        }

        if (tags.Any(tag => tag.Contains('<') || tag.Contains('>')))
        {
            result.Error("tags", "YouTube does not allow '<' or '>' in tags");
        }
    }

    private static void ValidateCategory(VideoSettings settings, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(settings.Category))
        {
            return;
        }

        if (CategoryRegistry.Resolve(settings.Category) is null)
        {
            result.Error(
                "category",
                $"'{settings.Category}' is not a known YouTube category. Known categories: {CategoryRegistry.DescribeKnownNames()}");
        }
    }

    private static void ValidateSchedule(VideoSettings settings, ValidationResult result, DateTimeOffset now)
    {
        if (settings.PublishAt is not { } publishAt)
        {
            return;
        }

        if (publishAt <= now)
        {
            result.Error("publishAt", $"'{publishAt:u}' is in the past, a scheduled publication must be in the future");
        }
        else if (publishAt - now < MinimumSchedulingLeadTime)
        {
            result.Warn(
                "publishAt",
                $"'{publishAt:u}' is less than {MinimumSchedulingLeadTime.TotalMinutes:0} minutes away, YouTube may publish the video immediately");
        }

        if (settings.Privacy is not null and not Configuration.Privacy.Private)
        {
            result.Error("publishAt", "a scheduled publication requires privacy 'private'");
        }
    }

    private static void ValidateRecording(VideoSettings settings, ValidationResult result, DateTimeOffset now)
    {
        if (settings.RecordingDate is { } recordedAt)
        {
            if (recordedAt > now)
            {
                result.Error("recordingDate", $"'{recordedAt:u}' is in the future");
            }
            else if (recordedAt < FirstSupportedRecordingDate)
            {
                result.Warn(
                    "recordingDate",
                    $"'{recordedAt:u}' is before {FirstSupportedRecordingDate:yyyy-MM-dd}, YouTube may reject it");
            }
        }

        if (string.IsNullOrWhiteSpace(settings.Location))
        {
            return;
        }

        if (!GeoPointParser.TryParse(settings.Location, out _, out _, out _))
        {
            result.Error("location", $"'{settings.Location}' is not a valid location, expected 'latitude,longitude[,altitude]'");
        }
        else if (settings.RecordingDate is null)
        {
            result.Warn("location", "YouTube usually ignores a location that is sent without a recordingDate");
        }
    }

    private static void ValidateLanguages(VideoSettings settings, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(settings.Language) && !LanguageCodes.IsValid(settings.Language))
        {
            result.Error("language", $"'{settings.Language}' is not a valid language code such as 'en' or 'en-US'");
        }

        if (!string.IsNullOrWhiteSpace(settings.AudioLanguage) && !LanguageCodes.IsValid(settings.AudioLanguage))
        {
            result.Error("audioLanguage", $"'{settings.AudioLanguage}' is not a valid language code such as 'en' or 'en-US'");
        }
    }

    private static void ValidateLocalizations(VideoSettings settings, ValidationResult result)
    {
        var localizations = settings.Localizations;
        if (localizations is not { Count: > 0 })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            result.Error("localizations", "localizations require 'language' to be set as well");
        }

        var defaultLanguage = LanguageCodes.Normalize(settings.Language);

        foreach (var (language, localization) in localizations)
        {
            if (!LanguageCodes.IsValid(language))
            {
                result.Error("localizations", $"'{language}' is not a valid language code");
                continue;
            }

            if (defaultLanguage is not null && string.Equals(LanguageCodes.Normalize(language), defaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                result.Warn("localizations", $"'{language}' is the default language of the video and has no effect");
            }

            if (localization is null)
            {
                result.Error("localizations", $"'{language}' has no title or description");
                continue;
            }

            if (localization.Title is { Length: > VideoMetadataFactory.MaxTitleLength })
            {
                result.Error("localizations", $"the title of '{language}' is too long");
            }

            if (localization.Description is { Length: > VideoMetadataFactory.MaxDescriptionLength })
            {
                result.Error("localizations", $"the description of '{language}' is too long");
            }

            if (localization.Title is null && localization.Description is null)
            {
                result.Error("localizations", $"'{language}' has no title or description");
            }
        }
    }

    private static void ValidateThumbnail(string? thumbnailPath, ValidationResult result)
    {
        if (thumbnailPath is null)
        {
            return;
        }

        if (!File.Exists(thumbnailPath))
        {
            result.Warn("thumbnail", $"'{thumbnailPath}' does not exist, the upload continues without it");
        }
    }
}