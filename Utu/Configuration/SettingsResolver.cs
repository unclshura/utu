namespace Utu.Configuration;

/// <summary>Merges video entries with the defaults of the configuration.</summary>
public static class SettingsResolver
{
    /// <summary>
    /// Merges <paramref name="entry"/> over <paramref name="defaults"/> and
    /// applies the utu defaults for the settings that the configuration normally
    /// leaves empty.
    /// </summary>
    public static VideoSettings Resolve(VideoEntry entry, VideoSettings? defaults) =>
        Resolve(entry.ToSettings(), defaults);

    /// <summary>
    /// Merges <paramref name="settings"/> over <paramref name="defaults"/> and
    /// applies the utu defaults for the settings that the configuration normally
    /// leaves empty.
    /// </summary>
    public static VideoSettings Resolve(VideoSettings settings, VideoSettings? defaults)
    {
        var resolved = settings.WithFallback(defaults);

        resolved.Privacy ??= Configuration.Privacy.Private;
        resolved.License ??= VideoLicense.Youtube;
        resolved.Embeddable ??= true;
        resolved.ShowLikeCount ??= true;
        resolved.AiUse ??= false;
        resolved.MadeForKids ??= false;

        return resolved;
    }
}