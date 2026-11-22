namespace Utu.Configuration;

/// <summary>
/// The global <c>utu.json</c> of the user (<c>%USERPROFILE%\.utu\utu.json</c>),
/// which <c>utu init</c> writes once so that the user has a file to edit.
/// </summary>
/// <remarks>
/// The template names every setting utu understands together with the value utu
/// would use anyway, so the file doubles as documentation. An empty string, an
/// empty list or a missing value still means "use the built in default".
/// Only settings that do not describe the individual video (such as
/// <c>file</c>, <c>enabled</c>, <c>recordingDate</c> and <c>publishAt</c>) are
/// left out of the template.
/// </remarks>
public static class GlobalConfiguration
{
    /// <summary>Returns the document written to a missing global configuration.</summary>
    public static UtuConfig CreateTemplate() => new()
    {
        Channel = new ChannelConfig
        {
            Name = string.Empty,
            ClientSecretsFile = ChannelSettings.DefaultClientSecretsFile,
            TokenCacheDirectory = ChannelSettings.DefaultTokenCacheDirectory,
            StateFile = string.Empty,
            User = ChannelSettings.DefaultUser,
            ParallelUploads = ChannelSettings.DefaultParallelUploads,
            ResumeUploads = true,
        },
        Defaults = new DefaultsSettings
        {
            Title = string.Empty,
            Description = string.Empty,
            Tags = [],
            Thumbnail = string.Empty,
            Playlists = [],
            Category = "Music",
            MadeForKids = false,
            Privacy = Privacy.Private,
            Location = string.Empty,
            LocationDescription = string.Empty,
            PaidPromotion = false,
            BrandPartnership = string.Empty,
            AiUse = false,
            Language = string.Empty,
            AudioLanguage = string.Empty,
            Localizations = [],
            License = VideoLicense.Youtube,
            Embeddable = true,
            ShowLikeCount = true,
            ShortsRemixing = Configuration.ShortsRemixing.VideoAndAudio,
            Comments = CommentPolicy.On,
            Moderation = CommentModeration.Basic,
            WhoCanComment = CommentAudience.Everyone,
            FeaturedPlaces = [],
            AllowAutomaticPlaces = true,
            Collaboration = [],
            PublishSchedule = string.Empty,
        },
        Videos = [],
    };

    /// <summary>Writes the template when <paramref name="path" /> does not exist yet.</summary>
    /// <param name="path">Absolute path of the global configuration file.</param>
    /// <returns><see langword="true" /> when the file was created.</returns>
    public static bool EnsureExists(string path)
    {
        if (File.Exists(path))
        {
            return false;
        }

        ConfigIO.Save(path, CreateTemplate());
        return true;
    }
}