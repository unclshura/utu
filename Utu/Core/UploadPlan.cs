using Utu.Configuration;

namespace Utu.Core;

/// <summary>What utu intends to do with one video.</summary>
public enum UploadAction
{
    /// <summary>Upload the video as a new video.</summary>
    Upload,

    /// <summary>Continue an interrupted upload.</summary>
    Resume,

    /// <summary>Add the thumbnail and playlists of an already uploaded video.</summary>
    ApplyExtras,

    /// <summary>Nothing to do.</summary>
    Skip,

    /// <summary>The video cannot be processed.</summary>
    Blocked,
}

/// <summary>Work that is still missing after a successful video upload.</summary>
/// <param name="ThumbnailPath">Thumbnail to set, or <see langword="null" />.</param>
/// <param name="Playlists">Playlists the video is not part of yet.</param>
public sealed record PendingExtras(string? ThumbnailPath, IReadOnlyList<string> Playlists)
{
    /// <summary>No pending work.</summary>
    public static PendingExtras None { get; } = new(null, []);

    public bool IsEmpty => ThumbnailPath is null && Playlists.Count == 0;
}

/// <summary>The decision for one entry of <c>videos</c>.</summary>
public sealed class UploadPlanItem
{
    /// <summary>Video file as written in the configuration.</summary>
    public required string File { get; init; }

    /// <summary>Absolute path of the video file.</summary>
    public required string FullPath { get; init; }

    /// <summary>What will be done.</summary>
    public required UploadAction Action { get; init; }

    /// <summary>Why this action was chosen.</summary>
    public string? Reason { get; init; }

    /// <summary>Resolved settings of the video.</summary>
    public VideoSettings? Settings { get; init; }

    /// <summary>Absolute path of the thumbnail, when one is configured.</summary>
    public string? ThumbnailPath { get; init; }

    /// <summary>Playlists configured for the video.</summary>
    public IReadOnlyList<string> Playlists { get; init; } = [];

    /// <summary>Work that is still missing for this video.</summary>
    public PendingExtras Extras { get; init; } = PendingExtras.None;

    /// <summary>Id of the video, when it has already been uploaded.</summary>
    public string? ExistingVideoId { get; init; }

    /// <summary>Resumable session to continue.</summary>
    public string? ResumableSessionUri { get; init; }

    /// <summary>Bytes YouTube has already received for <see cref="ResumableSessionUri" />.</summary>
    public long BytesSent { get; init; }

    /// <summary>Size of the video file in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Validation problems and warnings.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];

    /// <summary>Settings that have to be applied manually in YouTube Studio.</summary>
    public IReadOnlyList<string> ManualFollowUps { get; init; } = [];

    /// <summary>Title of the video, if known.</summary>
    public string? Title => Settings?.Title;

    /// <summary>Whether the item still needs work.</summary>
    public bool IsWork => Action is UploadAction.Upload or UploadAction.Resume or UploadAction.ApplyExtras;

    /// <summary>Whether the item is rejected.</summary>
    public bool IsBlocked => Action == UploadAction.Blocked;
}

/// <summary>The full upload plan.</summary>
public sealed class UploadPlan
{
    /// <summary>One item per configuration entry, in configuration order.</summary>
    public required IReadOnlyList<UploadPlanItem> Items { get; init; }

    /// <summary>Items that still need work.</summary>
    public IEnumerable<UploadPlanItem> Work => Items.Where(item => item.IsWork);

    /// <summary>Items that are rejected.</summary>
    public IEnumerable<UploadPlanItem> Blocked => Items.Where(item => item.IsBlocked);

    /// <summary>Whether there is anything to do.</summary>
    public bool HasWork => Work.Any();

    /// <summary>Number of items with the given action.</summary>
    public int CountOf(UploadAction action) => Items.Count(item => item.Action == action);
}