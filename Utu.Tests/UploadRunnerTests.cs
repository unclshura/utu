using Utu.Configuration;
using Utu.Core;
using Utu.YouTubeApi;

namespace Utu.Tests;

[TestClass]
public sealed class UploadRunnerTests
{
    [TestMethod]
    public async Task A_successful_upload_records_everything()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        folder.WriteFile("a.jpg");
        var config = TestConfig.With(new VideoEntry
        {
            File = "a.mp4",
            Title = "Song",
            Thumbnail = "a.jpg",
            Playlists = ["Songs"],
        });
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient();

        var summary = await Run(folder, config, state, client, Options());

        var outcome = summary.Outcomes.Single();
        Assert.AreEqual(UploadOutcomeStatus.Uploaded, outcome.Status);
        Assert.IsTrue(outcome.Succeeded);
        Assert.IsNull(outcome.Error);
        Assert.AreEqual("vid-a.mp4", outcome.VideoId);
        StringAssert.Contains(outcome.VideoUrl!, "vid-a.mp4");

        var upload = client.Uploads.Single();
        Assert.AreEqual(folder["a.mp4"], upload.FilePath);
        Assert.AreEqual("Song", upload.Body.Snippet.Title);
        StringAssert.Contains(upload.Parts, "snippet");
        StringAssert.Contains(upload.Parts, "status");
        Assert.IsNull(upload.ResumableSessionUri);

        Assert.AreEqual(("vid-a.mp4", folder["a.jpg"]), client.Thumbnails.Single());
        Assert.AreEqual(("Songs", "vid-a.mp4"), client.Playlists.Single());
        CollectionAssert.Contains(outcome.Notes.ToArray(), "added to playlist 'Songs' (pl-Songs)");
        Assert.IsFalse(outcome.Notes.Any(note => note.StartsWith("manual:", StringComparison.Ordinal)));

        var record = state.Find("a.mp4")!;
        Assert.AreEqual(UploadStatus.Completed, record.Status);
        Assert.AreEqual("vid-a.mp4", record.VideoId);
        Assert.AreEqual(7, record.BytesSent);
        Assert.AreEqual(7, record.FileSize);
        Assert.IsTrue(record.ThumbnailApplied);
        CollectionAssert.AreEqual(new[] { "Songs" }, record.PlaylistsApplied.ToArray());
        Assert.IsNull(record.ResumableSessionUri);
        Assert.IsNull(record.LastError);

        var reloaded = UploadStateStore.Load(folder[".utu/state.json"]).Document;
        Assert.AreEqual(UploadStatus.Completed, reloaded.Videos["a.mp4"].Status);
    }

    [TestMethod]
    public async Task Transient_failures_are_retried()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var client = new FakeYouTubeClient();
        var attempts = 0;
        client.OnUpload = _ => ++attempts < 3
            ? throw new YouTubeApiException("temporary", 503)
            : Task.FromResult(new UploadVideoResult("vid1"));

        var summary = await Run(folder, TestConfig.With(TestConfig.Entry("a.mp4")), client, Options());

        Assert.AreEqual(3, client.Uploads.Count);
        Assert.AreEqual(UploadOutcomeStatus.Uploaded, summary.Outcomes.Single().Status);
        Assert.AreEqual("vid1", summary.Outcomes.Single().VideoId);
    }

    [TestMethod]
    public async Task A_transient_failure_that_keeps_failing_gives_up_after_the_retries()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient
        {
            OnUpload = _ => throw new YouTubeApiException("temporary", 503),
        };

        var summary = await Run(folder, TestConfig.With(TestConfig.Entry("a.mp4")), state, client, Options(retries: 2));

        Assert.AreEqual(2, client.Uploads.Count);
        var outcome = summary.Outcomes.Single();
        Assert.AreEqual(UploadOutcomeStatus.Failed, outcome.Status);
        Assert.AreEqual("temporary", outcome.Error);
        Assert.IsTrue(summary.HasFailures);

        var record = state.Find("a.mp4")!;
        Assert.AreEqual(UploadStatus.Failed, record.Status);
        Assert.AreEqual("temporary", record.LastError);
        Assert.AreEqual("https://upload.example/a.mp4", record.ResumableSessionUri);
    }

    [TestMethod]
    public async Task A_rejected_upload_is_not_retried_and_is_recorded_as_a_failure()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient
        {
            OnUpload = _ => throw new YouTubeApiException("the request was rejected", 400, "invalidTitle"),
        };

        var summary = await Run(folder, TestConfig.With(TestConfig.Entry("a.mp4")), state, client, Options());

        Assert.AreEqual(1, client.Uploads.Count);
        Assert.AreEqual("the request was rejected", summary.Outcomes.Single().Error);

        var record = state.Find("a.mp4")!;
        Assert.AreEqual(UploadStatus.Failed, record.Status);
        Assert.AreEqual("the request was rejected", record.LastError);
        Assert.AreEqual("https://upload.example/a.mp4", record.ResumableSessionUri);
    }

    [TestMethod]
    public async Task A_thumbnail_failure_only_adds_a_note()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("a.jpg");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient
        {
            OnThumbnail = (_, _) => throw new YouTubeApiException("bad image", 400),
        };

        var summary = await Run(folder, ConfigWithThumbnail(), state, client, Options());

        var outcome = summary.Outcomes.Single();
        Assert.AreEqual(UploadOutcomeStatus.Uploaded, outcome.Status);
        CollectionAssert.Contains(outcome.Notes.ToArray(), "thumbnail not set: bad image");
        Assert.IsFalse(state.Find("a.mp4")!.ThumbnailApplied);
    }

    [TestMethod]
    public async Task A_playlist_failure_only_adds_a_note()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient
        {
            OnPlaylist = (_, _) => throw new YouTubeApiException("no such playlist", 404),
        };

        var summary = await Run(
            folder,
            TestConfig.With(new VideoEntry { File = "a.mp4", Title = "T", Playlists = ["Songs"] }),
            state,
            client,
            Options());

        var outcome = summary.Outcomes.Single();
        Assert.AreEqual(UploadOutcomeStatus.Uploaded, outcome.Status);
        CollectionAssert.Contains(outcome.Notes.ToArray(), "not added to playlist 'Songs': no such playlist");
        Assert.AreEqual(0, state.Find("a.mp4")!.PlaylistsApplied.Count);
    }

    [TestMethod]
    public async Task An_interrupted_upload_is_continued()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"], Document(Interrupted(folder, "a.mp4")));
        var client = new FakeYouTubeClient();

        var summary = await Run(folder, TestConfig.With(TestConfig.Entry("a.mp4")), state, client, Options());

        Assert.AreEqual("https://upload.example/previous", client.Uploads.Single().ResumableSessionUri);
        Assert.AreEqual(UploadOutcomeStatus.Resumed, summary.Outcomes.Single().Status);
        Assert.AreEqual(UploadStatus.Completed, state.Find("a.mp4")!.Status);
    }

    [TestMethod]
    public async Task An_interrupted_upload_keeps_its_session_for_the_next_run()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient
        {
            OnUpload = _ => throw new OperationCanceledException(),
        };

        var summary = await Run(folder, TestConfig.With(TestConfig.Entry("a.mp4")), state, client, Options());

        var outcome = summary.Outcomes.Single();
        Assert.AreEqual(UploadOutcomeStatus.Failed, outcome.Status);
        StringAssert.Contains(outcome.Error!, "interrupted");

        var record = state.Find("a.mp4")!;
        Assert.AreEqual(UploadStatus.Uploading, record.Status);
        Assert.AreEqual("interrupted", record.LastError);
        Assert.AreEqual("https://upload.example/a.mp4", record.ResumableSessionUri);

        var reloaded = UploadStateStore.Load(folder[".utu/state.json"]).Document;
        Assert.AreEqual("https://upload.example/a.mp4", reloaded.Videos["a.mp4"].ResumableSessionUri);
    }

    [TestMethod]
    public async Task Missing_thumbnail_and_playlists_are_added_without_uploading_again()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("a.jpg");
        var completed = Completed(folder, "a.mp4", "vid1");
        var state = new UploadStateStore(folder[".utu/state.json"], Document(completed));
        var client = new FakeYouTubeClient();
        var config = ConfigWithThumbnail();
        config.Videos[0].Playlists = ["Songs"];

        var summary = await Run(folder, config, state, client, Options());

        Assert.AreEqual(UploadOutcomeStatus.Updated, summary.Outcomes.Single().Status);
        Assert.AreEqual(0, client.Uploads.Count);
        Assert.AreEqual("vid1", client.Thumbnails.Single().VideoId);
        Assert.AreEqual(("Songs", "vid1"), client.Playlists.Single());

        var record = state.Find("a.mp4")!;
        Assert.IsTrue(record.ThumbnailApplied);
        CollectionAssert.AreEqual(new[] { "Songs" }, record.PlaylistsApplied.ToArray());
    }

    [TestMethod]
    public async Task Videos_are_uploaded_in_parallel_up_to_the_limit()
    {
        using var folder = new TempFolder();
        var entries = new List<VideoEntry>();
        for (var index = 0; index < 4; index++)
        {
            var name = $"video{index}.mp4";
            folder.WriteFile(name);
            entries.Add(TestConfig.Entry(name));
        }

        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient { UploadDelay = TimeSpan.FromMilliseconds(30) };

        var summary = await Run(folder, TestConfig.With([.. entries]), state, client, Options(parallelism: 2));

        Assert.AreEqual(4, summary.Outcomes.Count);
        Assert.IsTrue(summary.Outcomes.All(outcome => outcome.Status == UploadOutcomeStatus.Uploaded));
        Assert.AreEqual(4, client.Uploads.Count);
        Assert.IsTrue(client.MaxConcurrentUploads <= 2, $"at most 2 uploads at once, saw {client.MaxConcurrentUploads}");
    }

    [TestMethod]
    public async Task The_observer_sees_the_plan_the_progress_and_the_results()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var observer = new RecordingObserver();
        var plan = Plan(folder, state, TestConfig.With(TestConfig.Entry("a.mp4")));

        var summary = await new UploadRunner(new FakeYouTubeClient(), state, Options(), observer).RunAsync(plan);

        Assert.AreEqual(1, observer.PlanReadyCalls);
        Assert.AreEqual(1, observer.Started.Count);
        Assert.AreEqual("a.mp4", observer.Started[0].File);
        Assert.IsTrue(observer.Progress.Any(progress => progress.BytesSent == 1));
        Assert.AreEqual(summary.Outcomes.Single(), observer.Finished.Single());
    }

    [TestMethod]
    public async Task A_plan_without_work_does_nothing()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var state = new UploadStateStore(folder[".utu/state.json"]);
        var client = new FakeYouTubeClient();
        var observer = new RecordingObserver();
        var config = TestConfig.With(new VideoEntry { File = "a.mp4", Title = "T", Enabled = false });
        var plan = Plan(folder, state, config);

        var summary = await new UploadRunner(client, state, Options(), observer).RunAsync(plan);

        Assert.AreEqual(0, summary.Outcomes.Count);
        Assert.IsFalse(summary.HasFailures);
        Assert.AreEqual(0, client.Uploads.Count);
        Assert.AreEqual(1, observer.PlanReadyCalls);
        Assert.AreEqual(0, observer.Started.Count);
        Assert.IsTrue(File.Exists(folder[".utu/state.json"]));
    }

    private static UploadRunnerOptions Options(int parallelism = 1, int retries = 3) => new()
    {
        Parallelism = parallelism,
        Retries = retries,
        RetryDelay = TimeSpan.Zero,
        MaxRetryDelay = TimeSpan.Zero,
    };

    private static UtuConfig ConfigWithThumbnail() =>
        TestConfig.With(new VideoEntry { File = "a.mp4", Title = "T", Thumbnail = "a.jpg" });

    private static UploadPlan Plan(TempFolder folder, UploadStateStore state, UtuConfig config) =>
        new UploadPlanner(state).CreatePlan(config, folder.Path);

    private static Task<UploadSummary> Run(
        TempFolder folder,
        UtuConfig config,
        UploadStateStore state,
        FakeYouTubeClient client,
        UploadRunnerOptions options) =>
        new UploadRunner(client, state, options).RunAsync(Plan(folder, state, config));

    private static Task<UploadSummary> Run(
        TempFolder folder,
        UtuConfig config,
        FakeYouTubeClient client,
        UploadRunnerOptions options)
    {
        var state = new UploadStateStore(folder[".utu/state.json"]);
        return new UploadRunner(client, state, options).RunAsync(Plan(folder, state, config));
    }

    private static VideoUploadState Interrupted(TempFolder folder, string file)
    {
        var identity = FileIdentity.FromFile(folder[file]);
        return new VideoUploadState
        {
            File = file,
            Status = UploadStatus.Uploading,
            ResumableSessionUri = "https://upload.example/previous",
            BytesSent = 4,
            FileSize = identity.Size,
            FileWriteTimeUtcTicks = identity.WriteTimeUtcTicks,
        };
    }

    private static VideoUploadState Completed(TempFolder folder, string file, string videoId)
    {
        var identity = FileIdentity.FromFile(folder[file]);
        return new VideoUploadState
        {
            File = file,
            Status = UploadStatus.Completed,
            VideoId = videoId,
            FileSize = identity.Size,
            FileWriteTimeUtcTicks = identity.WriteTimeUtcTicks,
        };
    }

    private static UploadStateDocument Document(VideoUploadState state)
    {
        var document = new UploadStateDocument();
        document.Videos[UploadStateStore.NormalizeKey(state.File!)] = state;
        return document;
    }

    /// <summary>A YouTube client that answers from memory.</summary>
    private sealed class FakeYouTubeClient : IYouTubeClient
    {
        private readonly object _gate = new();
        private readonly List<UploadVideoRequest> _uploads = [];
        private readonly List<(string VideoId, string ImagePath)> _thumbnails = [];
        private readonly List<(string Playlist, string VideoId)> _playlists = [];
        private int _activeUploads;
        private int _maxConcurrentUploads;

        public Func<UploadVideoRequest, Task<UploadVideoResult>>? OnUpload { get; set; }

        public Func<string, string, Task>? OnThumbnail { get; set; }

        public Func<string, string, Task<string>>? OnPlaylist { get; set; }

        public TimeSpan UploadDelay { get; set; }

        public IReadOnlyList<UploadVideoRequest> Uploads
        {
            get
            {
                lock (_gate)
                {
                    return [.. _uploads];
                }
            }
        }

        public IReadOnlyList<(string VideoId, string ImagePath)> Thumbnails
        {
            get
            {
                lock (_gate)
                {
                    return [.. _thumbnails];
                }
            }
        }

        public IReadOnlyList<(string Playlist, string VideoId)> Playlists
        {
            get
            {
                lock (_gate)
                {
                    return [.. _playlists];
                }
            }
        }

        public int MaxConcurrentUploads => Volatile.Read(ref _maxConcurrentUploads);

        public async Task<UploadVideoResult> UploadVideoAsync(
            UploadVideoRequest request,
            IProgress<UploadProgressInfo>? progress,
            Action<string>? sessionUriCaptured,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _activeUploads);
            RecordConcurrency(active);

            try
            {
                lock (_gate)
                {
                    _uploads.Add(request);
                }

                sessionUriCaptured?.Invoke($"https://upload.example/{Path.GetFileName(request.FilePath)}");
                progress?.Report(new UploadProgressInfo(1, UploadStage.Uploading));

                if (UploadDelay > TimeSpan.Zero)
                {
                    await Task.Delay(UploadDelay, cancellationToken).ConfigureAwait(false);
                }

                var handler = OnUpload;
                var result = handler is null
                    ? new UploadVideoResult($"vid-{Path.GetFileName(request.FilePath)}")
                    : await handler(request).ConfigureAwait(false);

                progress?.Report(new UploadProgressInfo(new FileInfo(request.FilePath).Length, UploadStage.Completed));
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref _activeUploads);
            }
        }

        public Task SetThumbnailAsync(string videoId, string imagePath, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _thumbnails.Add((videoId, imagePath));
            }

            return OnThumbnail is { } handler ? handler(videoId, imagePath) : Task.CompletedTask;
        }

        public Task<string> AddToPlaylistAsync(string playlist, string videoId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _playlists.Add((playlist, videoId));
            }

            return OnPlaylist is { } handler
                ? handler(playlist, videoId)
                : Task.FromResult($"pl-{playlist}");
        }

        public Task<string?> GetChannelTitleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("Test channel");

        private void RecordConcurrency(int active)
        {
            int current;
            while (active > (current = Volatile.Read(ref _maxConcurrentUploads)))
            {
                if (Interlocked.CompareExchange(ref _maxConcurrentUploads, active, current) == current)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Records everything the runner reports.</summary>
    private sealed class RecordingObserver : IUploadObserver
    {
        private readonly object _gate = new();
        private readonly List<UploadPlanItem> _started = [];
        private readonly List<UploadOutcome> _finished = [];
        private readonly List<(string File, long BytesSent)> _progress = [];

        public int PlanReadyCalls { get; private set; }

        public UploadPlan? Plan { get; private set; }

        public IReadOnlyList<UploadPlanItem> Started
        {
            get
            {
                lock (_gate)
                {
                    return [.. _started];
                }
            }
        }

        public IReadOnlyList<UploadOutcome> Finished
        {
            get
            {
                lock (_gate)
                {
                    return [.. _finished];
                }
            }
        }

        public IReadOnlyList<(string File, long BytesSent)> Progress
        {
            get
            {
                lock (_gate)
                {
                    return [.. _progress];
                }
            }
        }

        public void PlanReady(UploadPlan plan)
        {
            lock (_gate)
            {
                PlanReadyCalls++;
                Plan = plan;
            }
        }

        public void ItemStarted(UploadPlanItem item)
        {
            lock (_gate)
            {
                _started.Add(item);
            }
        }

        public void ItemProgress(UploadPlanItem item, long bytesSent)
        {
            lock (_gate)
            {
                _progress.Add((item.File, bytesSent));
            }
        }

        public void ItemFinished(UploadOutcome outcome)
        {
            lock (_gate)
            {
                _finished.Add(outcome);
            }
        }
    }
}
