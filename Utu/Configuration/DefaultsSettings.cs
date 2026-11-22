namespace Utu.Configuration;

/// <summary>
/// The <c>defaults</c> block of a configuration file: the settings that every
/// video inherits plus the settings that only describe the folder as a whole.
/// A video entry is a <see cref="VideoSettings" /> and therefore cannot carry a
/// <see cref="PublishSchedule" />.
/// </summary>
public sealed class DefaultsSettings : VideoSettings
{
    /// <summary>
    /// Crontab expression that <c>utu init</c> uses to spread the publication
    /// times of the videos: the first video is published at the first time the
    /// expression matches after the run, the second at the time after that, and
    /// so on. Unset leaves <see cref="VideoSettings.PublishAt" /> empty, so
    /// nothing is scheduled.
    /// </summary>
    public string? PublishSchedule { get; set; }
}
