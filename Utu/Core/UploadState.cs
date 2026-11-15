using System.Text.Json.Serialization;

namespace Utu.Core;

/// <summary>Progress of one video, as recorded in the state file.</summary>
public enum UploadStatus
{
    /// <summary>Never attempted.</summary>
    Pending,

    /// <summary>Interrupted or still running.</summary>
    Uploading,

    /// <summary>Uploaded successfully.</summary>
    Completed,

    /// <summary>Failed; a later run retries it.</summary>
    Failed,
}

/// <summary>Recorded progress of a single video.</summary>
public sealed class VideoUploadState
{
    /// <summary>Video file this record belongs to, relative to the working folder.</summary>
    public string? File { get; set; }

    /// <summary>Last known status.</summary>
    public UploadStatus Status { get; set; }

    /// <summary>Id of the uploaded video.</summary>
    public string? VideoId { get; set; }

    /// <summary>Resumable session of an interrupted upload.</summary>
    public string? ResumableSessionUri { get; set; }

    /// <summary>Bytes of the file that YouTube has received.</summary>
    public long BytesSent { get; set; }

    /// <summary>Size of the file when the upload started.</summary>
    public long FileSize { get; set; }

    /// <summary>Last write time of the file when the upload started.</summary>
    public long FileWriteTimeUtcTicks { get; set; }

    /// <summary>Whether the thumbnail has been applied.</summary>
    public bool ThumbnailApplied { get; set; }

    /// <summary>Playlists the video has been added to.</summary>
    public List<string> PlaylistsApplied { get; set; } = [];

    /// <summary>Message of the last failure.</summary>
    public string? LastError { get; set; }

    /// <summary>When this record was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Identity of the file that was uploaded.</summary>
    [JsonIgnore]
    public FileIdentity Identity => new(FileSize, FileWriteTimeUtcTicks);

    /// <summary>Whether <paramref name="identity" /> matches the uploaded file.</summary>
    public bool Matches(FileIdentity identity) => Identity == identity;
}

/// <summary>Content of the state file.</summary>
public sealed class UploadStateDocument
{
    /// <summary>Format version, for future migrations.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Records keyed by the normalized relative path of the video.</summary>
    public Dictionary<string, VideoUploadState> Videos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Result of reading the state file.</summary>
/// <param name="Document">The state, empty when it could not be read.</param>
/// <param name="Warning">Why the state had to be discarded, if it had to be.</param>
public sealed record UploadStateLoadResult(UploadStateDocument Document, string? Warning);