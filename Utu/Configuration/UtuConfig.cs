using System.Text.Json.Serialization;

namespace Utu.Configuration;

/// <summary>Root of the <c>utu.json</c> document.</summary>
public sealed class UtuConfig
{
    /// <summary>Optional schema reference, ignored by utu.</summary>
    [JsonPropertyName("$schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>
    /// Channel wide settings, including the OAuth client to use. Usually only the
    /// global configuration defines them.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChannelConfig? Channel { get; set; }

    /// <summary>Settings inherited by every entry of <see cref="Videos" />.</summary>
    public VideoSettings Defaults { get; set; } = new();

    /// <summary>The videos to upload. Only a folder configuration has videos.</summary>
    public List<VideoEntry> Videos { get; set; } = [];
}

/// <summary>
/// Channel wide upload settings. Every property is optional, so that a folder
/// configuration only carries the values that differ from the global
/// configuration (<c>%USERPROFILE%\.utu\utu.json</c>).
/// </summary>
public sealed class ChannelConfig
{
    /// <summary>Informational name of the channel, shown in reports.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// OAuth client secrets of an installed ("Desktop app") client, relative to
    /// the folder of the configuration file that defines it.
    /// </summary>
    public string? ClientSecretsFile { get; set; }

    /// <summary>
    /// Folder that caches the OAuth token, relative to the folder of the
    /// configuration file that defines it.
    /// </summary>
    public string? TokenCacheDirectory { get; set; }

    /// <summary>
    /// File that records upload progress, relative to the folder of the
    /// configuration file that defines it.
    /// </summary>
    public string? StateFile { get; set; }

    /// <summary>Name of the cached OAuth token.</summary>
    public string? User { get; set; }

    /// <summary>Default number of parallel uploads.</summary>
    public int? ParallelUploads { get; set; }

    /// <summary>Whether failed or interrupted uploads are continued.</summary>
    public bool? ResumeUploads { get; set; }
}
