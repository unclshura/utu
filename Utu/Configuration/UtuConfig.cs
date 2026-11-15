using System.Text.Json.Serialization;

namespace Utu.Configuration;

/// <summary>Root of the <c>utu.json</c> document.</summary>
public sealed class UtuConfig
{
    /// <summary>Optional schema reference, ignored by utu.</summary>
    [JsonPropertyName("$schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>Channel wide settings, including the OAuth client to use.</summary>
    public ChannelConfig Channel { get; set; } = new();

    /// <summary>Settings inherited by every entry of <see cref="Videos"/>.</summary>
    public VideoSettings Defaults { get; set; } = new();

    /// <summary>The videos to upload.</summary>
    public List<VideoEntry> Videos { get; set; } = [];
}

/// <summary>Channel wide upload settings.</summary>
public sealed class ChannelConfig
{
    /// <summary>Informational name of the channel, shown in reports.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// OAuth client secrets of an installed ("Desktop app") client, relative to
    /// the working folder.
    /// </summary>
    public string ClientSecretsFile { get; set; } = "client_secret.json";

    /// <summary>Folder that caches the OAuth token, relative to the working folder.</summary>
    public string TokenCacheDirectory { get; set; } = ".utu/auth";

    /// <summary>File that records upload progress, relative to the working folder.</summary>
    public string StateFile { get; set; } = ".utu/state.json";

    /// <summary>Name of the cached OAuth token.</summary>
    public string User { get; set; } = "default";

    /// <summary>Default number of parallel uploads.</summary>
    public int ParallelUploads { get; set; } = 3;

    /// <summary>Whether failed or interrupted uploads are continued.</summary>
    public bool ResumeUploads { get; set; } = true;
}
