using Utu.Configuration;

namespace Utu.Core;

/// <summary>Checks a loaded configuration before anything is uploaded.</summary>
public static class UtuConfigValidator
{
    /// <summary>Checks the merged channel settings and the list of videos.</summary>
    public static ValidationResult Validate(ResolvedConfiguration config)
    {
        var result = new ValidationResult();
        CheckChannel(config.Channel, result);

        var videos = config.Videos;

        if (videos.Count == 0)
        {
            result.Warn("videos", "the configuration has no videos; run 'utu init'");
        }

        foreach (var duplicate in videos
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.File))
                     .GroupBy(entry => UploadStateStore.NormalizeKey(entry.File!), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            result.Error("videos", $"'{duplicate.Key}' is listed {duplicate.Count()} times");
        }

        return result;
    }

    /// <summary>Checks only the channel settings, as used by the global configuration.</summary>
    public static ValidationResult ValidateChannel(ChannelSettings channel)
    {
        var result = new ValidationResult();
        CheckChannel(channel, result);
        return result;
    }

    /// <summary>Checks a folder configuration that has no global configuration.</summary>
    /// <param name="config">Loaded configuration.</param>
    /// <param name="workingFolder">Folder of the configuration.</param>
    public static ValidationResult Validate(UtuConfig config, string workingFolder = ".") =>
        Validate(ResolvedConfiguration.Resolve(workingFolder, config));

    private static void CheckChannel(ChannelSettings channel, ValidationResult result)
    {
        if (channel.ParallelUploads < 1)
        {
            result.Error("channel.parallelUploads", $"must be at least 1 but is {channel.ParallelUploads}");
        }

        if (string.IsNullOrWhiteSpace(channel.Name))
        {
            result.Warn("channel.name", "is empty, the channel is only identified by the access token");
        }
    }
}