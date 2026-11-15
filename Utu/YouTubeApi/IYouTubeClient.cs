using Google.Apis.YouTube.v3.Data;

namespace Utu.YouTubeApi;

/// <summary>Phase of an upload, used for the progress display.</summary>
public enum UploadStage
{
    Uploading,
    Completed,
}

/// <summary>Progress of a single video upload.</summary>
/// <param name="BytesSent">Bytes acknowledged by YouTube so far.</param>
/// <param name="Stage">Current phase of the upload.</param>
public readonly record struct UploadProgressInfo(long BytesSent, UploadStage Stage);

/// <summary>Description of one <c>videos.insert</c> call.</summary>
public sealed class UploadVideoRequest
{
    /// <summary>Video file to read.</summary>
    public required string FilePath { get; init; }

    /// <summary>Metadata of the video.</summary>
    public required Video Body { get; init; }

    /// <summary>The <c>part</c> parameter that matches <see cref="Body" />.</summary>
    public required string Parts { get; init; }

    /// <summary>
    /// Resumable session of an interrupted upload. When set, the upload continues
    /// where it stopped instead of starting over.
    /// </summary>
    public string? ResumableSessionUri { get; init; }
}

/// <summary>Result of a successful <c>videos.insert</c> call.</summary>
/// <param name="VideoId">Id of the new video.</param>
public sealed record UploadVideoResult(string VideoId);

/// <summary>The subset of the YouTube Data API that utu needs.</summary>
public interface IYouTubeClient
{
    /// <summary>
    /// Uploads a video. When <see cref="UploadVideoRequest.ResumableSessionUri" />
    /// is set, an interrupted upload is continued.
    /// </summary>
    /// <param name="request">Video and metadata to upload.</param>
    /// <param name="progress">Receives upload progress.</param>
    /// <param name="sessionUriCaptured">
    /// Receives the resumable session URI, so that an interrupted upload can be
    /// continued by a later run.
    /// </param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    Task<UploadVideoResult> UploadVideoAsync(
        UploadVideoRequest request,
        IProgress<UploadProgressInfo>? progress,
        Action<string>? sessionUriCaptured,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the thumbnail of an uploaded video.</summary>
    Task SetThumbnailAsync(string videoId, string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a video to a playlist, specified by id or by name, and returns the id
    /// of the playlist that was used.
    /// </summary>
    Task<string> AddToPlaylistAsync(string playlist, string videoId, CancellationToken cancellationToken = default);

    /// <summary>Returns the title of the authenticated channel.</summary>
    Task<string?> GetChannelTitleAsync(CancellationToken cancellationToken = default);
}