using Google;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace Utu.YouTubeApi;

/// <summary><see cref="IYouTubeClient" /> on top of the YouTube Data API v3.</summary>
public sealed class YouTubeClient : IYouTubeClient
{
    /// <summary>Upload chunk size; must be a multiple of 256 KB.</summary>
    private const int UploadChunkSize = 8 * 1024 * 1024;

    private const int PlaylistsPerPage = 50;
    private const int MaxPlaylistPages = 20;

    private readonly YouTubeService _service;

    public YouTubeClient(YouTubeService service) => _service = service;

    /// <inheritdoc />
    public async Task<UploadVideoResult> UploadVideoAsync(
        UploadVideoRequest request,
        IProgress<UploadProgressInfo>? progress,
        Action<string>? sessionUriCaptured,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.FilePath))
        {
            throw new UtuException($"Video file not found: {request.FilePath}");
        }

        await using var stream = File.OpenRead(request.FilePath);

        var insert = _service.Videos.Insert(request.Body, request.Parts, stream, "video/*");
        insert.ChunkSize = UploadChunkSize;

        if (sessionUriCaptured is not null)
        {
            insert.UploadSessionData += data =>
            {
                if (data.UploadUri is { } uri)
                {
                    sessionUriCaptured(uri.ToString());
                }
            };
        }

        progress?.Report(new UploadProgressInfo(0, UploadStage.Uploading));
        insert.ProgressChanged += update => progress?.Report(new UploadProgressInfo(
            update.BytesSent,
            update.Status == UploadStatus.Completed ? UploadStage.Completed : UploadStage.Uploading));

        try
        {
            var resumable = request.ResumableSessionUri is { Length: > 0 } session && Uri.TryCreate(session, UriKind.Absolute, out var sessionUri)
                ? insert.ResumeAsync(sessionUri, cancellationToken)
                : insert.UploadAsync(cancellationToken);

            var result = await resumable.ConfigureAwait(false);
            if (result.Status != UploadStatus.Completed)
            {
                throw result.Exception is GoogleApiException apiException
                    ? YouTubeApiException.FromGoogleApiException(apiException, "video upload")
                    : new YouTubeApiException(
                        $"video upload failed: {result.Exception?.Message ?? result.Status.ToString()}",
                        innerException: result.Exception);
            }
        }
        catch (GoogleApiException exception)
        {
            throw YouTubeApiException.FromGoogleApiException(exception, "video upload");
        }

        var videoId = insert.ResponseBody?.Id;
        if (string.IsNullOrEmpty(videoId))
        {
            throw new YouTubeApiException("video upload failed: YouTube did not return a video id");
        }

        progress?.Report(new UploadProgressInfo(stream.Length, UploadStage.Completed));
        return new UploadVideoResult(videoId);
    }

    /// <inheritdoc />
    public async Task SetThumbnailAsync(string videoId, string imagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
        {
            throw new UtuException($"Thumbnail file not found: {imagePath}");
        }

        await using var stream = File.OpenRead(imagePath);

        try
        {
            var set = _service.Thumbnails.Set(videoId, stream, ThumbnailMime.FromPath(imagePath));
            var result = await set.UploadAsync(cancellationToken).ConfigureAwait(false);

            if (result.Status != UploadStatus.Completed)
            {
                throw result.Exception is GoogleApiException apiException
                    ? YouTubeApiException.FromGoogleApiException(apiException, "thumbnail upload")
                    : new YouTubeApiException(
                        $"thumbnail upload failed: {result.Exception?.Message ?? result.Status.ToString()}",
                        innerException: result.Exception);
            }
        }
        catch (GoogleApiException exception)
        {
            throw YouTubeApiException.FromGoogleApiException(exception, "thumbnail upload");
        }
    }

    /// <inheritdoc />
    public async Task<string> AddToPlaylistAsync(string playlist, string videoId, CancellationToken cancellationToken = default)
    {
        var playlistId = await ResolvePlaylistIdAsync(playlist, cancellationToken).ConfigureAwait(false);

        var item = new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet
            {
                PlaylistId = playlistId,
                ResourceId = new ResourceId { Kind = "youtube#video", VideoId = videoId },
            },
        };

        try
        {
            await _service.PlaylistItems.Insert(item, "snippet").ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GoogleApiException exception)
        {
            throw YouTubeApiException.FromGoogleApiException(exception, $"adding the video to playlist '{playlist}'");
        }

        return playlistId;
    }

    /// <inheritdoc />
    public async Task<string?> GetChannelTitleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = _service.Channels.List("snippet");
            request.Mine = true;

            var response = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return response.Items?.FirstOrDefault()?.Snippet?.Title;
        }
        catch (GoogleApiException exception)
        {
            throw YouTubeApiException.FromGoogleApiException(exception, "reading the channel");
        }
    }

    /// <summary>Returns playlist ids unchanged and looks names up in the channel.</summary>
    private async Task<string> ResolvePlaylistIdAsync(string playlist, CancellationToken cancellationToken)
    {
        var value = playlist.Trim();
        if (IsPlaylistId(value))
        {
            return value;
        }

        string? pageToken = null;
        for (var page = 0; page < MaxPlaylistPages; page++)
        {
            var request = _service.Playlists.List("snippet");
            request.Mine = true;
            request.MaxResults = PlaylistsPerPage;
            request.PageToken = pageToken;

            var response = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            var match = response.Items?
                .FirstOrDefault(item => string.Equals(item.Snippet?.Title?.Trim(), value, StringComparison.OrdinalIgnoreCase));

            if (match?.Id is { Length: > 0 } id)
            {
                return id;
            }

            pageToken = response.NextPageToken;
            if (string.IsNullOrEmpty(pageToken))
            {
                break;
            }
        }

        throw new YouTubeApiException($"playlist '{value}' not found on the channel");
    }

    /// <summary>
    /// YouTube playlist ids are long and start with a two letter prefix such as
    /// <c>PL</c> or <c>UU</c>; short values are treated as names.
    /// </summary>
    private static bool IsPlaylistId(string value) =>
        value.Length >= 18 && !value.Any(char.IsWhiteSpace);
}