using Utu.Configuration;

namespace Utu.Core;

/// <summary>Checks a loaded configuration before anything is uploaded.</summary>
public static class UtuConfigValidator
{
    /// <summary>Checks the channel settings and the list of videos.</summary>
    public static ValidationResult Validate(UtuConfig config)
    {
        var result = new ValidationResult();

        if (config.Channel.ParallelUploads < 1)
        {
            result.Error("channel.parallelUploads", "must be at least 1");
        }

        if (string.IsNullOrWhiteSpace(config.Channel.ClientSecretsFile))
        {
            result.Error("channel.clientSecretsFile", "is empty");
        }

        var videos = config.Videos ?? [];

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

        if (string.IsNullOrWhiteSpace(config.Channel.Name))
        {
            result.Warn("channel.name", "is empty, the channel is only identified by the access token");
        }

        return result;
    }
}