using Utu.Configuration;
using Utu.YouTubeApi;

namespace Utu.Core;

/// <summary>Settings of an upload run.</summary>
public sealed record UploadRunnerOptions
{
    /// <summary>Default number of attempts per operation.</summary>
    public const int DefaultRetries = 3;

    /// <summary>Number of videos uploaded at the same time.</summary>
    public int Parallelism { get; init; } = 3;

    /// <summary>Attempts per operation, including the first one.</summary>
    public int Retries { get; init; } = DefaultRetries;

    /// <summary>Delay before the second attempt; doubled for every further attempt.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound of the retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Runs an upload plan: uploads the videos in parallel, applies thumbnails and
/// playlists, and records everything in the state file.
/// </summary>
public sealed class UploadRunner
{
    private readonly IYouTubeClient _client;
    private readonly UploadStateStore _state;
    private readonly UploadRunnerOptions _options;
    private readonly IUploadObserver? _observer;
    private readonly object _observerGate = new();

    public UploadRunner(
        IYouTubeClient client,
        UploadStateStore state,
        UploadRunnerOptions? options = null,
        IUploadObserver? observer = null)
    {
        _client = client;
        _state = state;
        _options = options ?? new UploadRunnerOptions();
        _observer = observer;
    }

    /// <summary>Processes every item of <paramref name="plan" /> that needs work.</summary>
    public async Task<UploadSummary> RunAsync(UploadPlan plan, CancellationToken cancellationToken = default)
    {
        var work = plan.Work.ToList();
        var outcomes = new UploadOutcome?[work.Count];

        Notify(observer => observer.PlanReady(plan));

        if (work.Count > 0)
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, work.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.Parallelism) },
                async (index, _) =>
                {
                    var item = work[index];

                    try
                    {
                        outcomes[index] = await ExecuteAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        outcomes[index] = new UploadOutcome(
                            item,
                            UploadOutcomeStatus.Failed,
                            item.ExistingVideoId,
                            "interrupted, run utu again to continue the upload",
                            item.ManualFollowUps.Select(Manual).ToList());
                    }
                    catch (Exception exception)
                    {
                        outcomes[index] = new UploadOutcome(
                            item,
                            UploadOutcomeStatus.Failed,
                            item.ExistingVideoId,
                            exception.Message,
                            item.ManualFollowUps.Select(Manual).ToList());
                    }

                    Notify(observer => observer.ItemFinished(outcomes[index]!));
                });
        }

        _state.Save();

        var completed = outcomes.Where(outcome => outcome is not null).Select(outcome => outcome!).ToList();
        return new UploadSummary(completed);
    }

    private async Task<UploadOutcome> ExecuteAsync(UploadPlanItem item, CancellationToken cancellationToken)
    {
        var notes = item.ManualFollowUps.Select(Manual).ToList();
        var videoId = item.ExistingVideoId;

        if (item.Action != UploadAction.ApplyExtras)
        {
            videoId = await UploadVideoAsync(item, cancellationToken).ConfigureAwait(false);
        }

        if (videoId is not { Length: > 0 })
        {
            throw new UtuException("no video id is known for this item");
        }

        await ApplyThumbnailAsync(item, videoId, notes, cancellationToken).ConfigureAwait(false);
        await ApplyPlaylistsAsync(item, videoId, notes, cancellationToken).ConfigureAwait(false);

        var status = item.Action switch
        {
            UploadAction.ApplyExtras => UploadOutcomeStatus.Updated,
            UploadAction.Resume => UploadOutcomeStatus.Resumed,
            _ => UploadOutcomeStatus.Uploaded,
        };

        return new UploadOutcome(item, status, videoId, null, notes);
    }

    private async Task<string> UploadVideoAsync(UploadPlanItem item, CancellationToken cancellationToken)
    {
        var settings = item.Settings ?? new VideoSettings();
        var identity = FileIdentity.FromFile(item.FullPath);
        var session = item.ResumableSessionUri;
        var firstAttempt = true;

        Notify(observer => observer.ItemStarted(item));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var request = new UploadVideoRequest
                {
                    FilePath = item.FullPath,
                    Body = VideoMetadataFactory.CreateBody(settings),
                    Parts = VideoMetadataFactory.BuildParts(settings),
                    ResumableSessionUri = session,
                };

                _state.Update(
                    item.File,
                    state =>
                    {
                        state.File = item.File;
                        state.Status = UploadStatus.Uploading;
                        state.FileSize = identity.Size;
                        state.FileWriteTimeUtcTicks = identity.WriteTimeUtcTicks;
                        state.LastError = null;
                    },
                    persist: firstAttempt);

                firstAttempt = false;

                var result = await _client
                    .UploadVideoAsync(
                        request,
                        CreateProgress(item),
                        sessionUri => session = sessionUri,
                        cancellationToken)
                    .ConfigureAwait(false);

                // The session URI is only useful while the upload is running.
                session = null;

                _state.Update(
                    item.File,
                    state =>
                    {
                        state.Status = UploadStatus.Completed;
                        state.VideoId = result.VideoId;
                        state.ResumableSessionUri = null;
                        state.BytesSent = identity.Size;
                        state.FileSize = identity.Size;
                        state.FileWriteTimeUtcTicks = identity.WriteTimeUtcTicks;
                        state.ThumbnailApplied = false;
                        state.PlaylistsApplied.Clear();
                        state.LastError = null;
                    },
                    persist: true);

                return result.VideoId;
            }
            catch (YouTubeApiException exception) when (exception.IsTransient && attempt < Math.Max(1, _options.Retries))
            {
                await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Keep the status resumable so that the next run continues the upload.
                _state.Update(
                    item.File,
                    state =>
                    {
                        state.Status = UploadStatus.Uploading;
                        state.ResumableSessionUri = session;
                        state.LastError = "interrupted";
                    },
                    persist: true);

                throw;
            }
            catch (Exception exception)
            {
                _state.Update(
                    item.File,
                    state =>
                    {
                        state.Status = UploadStatus.Failed;
                        state.ResumableSessionUri = session;
                        state.LastError = exception.Message;
                    },
                    persist: true);

                throw;
            }
        }
    }

    private async Task ApplyThumbnailAsync(
        UploadPlanItem item,
        string videoId,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        if (item.Extras.ThumbnailPath is not { } thumbnailPath)
        {
            return;
        }

        try
        {
            await WithRetryAsync(
                    async () =>
                    {
                        await _client.SetThumbnailAsync(videoId, thumbnailPath, cancellationToken).ConfigureAwait(false);
                        return true;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            _state.Update(item.File, state => state.ThumbnailApplied = true, persist: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            notes.Add($"thumbnail not set: {exception.Message}");
        }
    }

    private async Task ApplyPlaylistsAsync(
        UploadPlanItem item,
        string videoId,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        foreach (var playlist in item.Extras.Playlists)
        {
            try
            {
                var playlistId = await WithRetryAsync(
                        () => _client.AddToPlaylistAsync(playlist, videoId, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);

                _state.Update(
                    item.File,
                    state =>
                    {
                        if (!state.PlaylistsApplied.Contains(playlist, StringComparer.OrdinalIgnoreCase))
                        {
                            state.PlaylistsApplied.Add(playlist);
                        }
                    },
                    persist: true);

                notes.Add($"added to playlist '{playlist}' ({playlistId})");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notes.Add($"not added to playlist '{playlist}': {exception.Message}");
            }
        }
    }

    private IProgress<UploadProgressInfo>? CreateProgress(UploadPlanItem item) =>
        _observer is null
            ? null
            : new InlineProgress<UploadProgressInfo>(info => Notify(observer => observer.ItemProgress(item, info.BytesSent)));

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (YouTubeApiException exception) when (exception.IsTransient && attempt < Math.Max(1, _options.Retries))
            {
                await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var ticks = Math.Min(
            _options.MaxRetryDelay.Ticks,
            _options.RetryDelay.Ticks * (1L << Math.Min(attempt - 1, 16)));

        await Task.Delay(TimeSpan.FromTicks(Math.Max(0, ticks)), cancellationToken).ConfigureAwait(false);
    }

    private void Notify(Action<IUploadObserver> action)
    {
        if (_observer is null)
        {
            return;
        }

        lock (_observerGate)
        {
            action(_observer);
        }
    }

    private static string Manual(string followUp) => $"manual: {followUp}";

    /// <summary>Reports progress synchronously, so that the order is preserved.</summary>
    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}