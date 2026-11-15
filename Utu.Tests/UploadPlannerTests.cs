using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class UploadPlannerTests
{
    [TestMethod]
    public void A_new_video_is_uploaded()
    {
        using var folder = new TempFolder();
        var file = folder.WriteFile("a.mp4", "content");
        var config = TestConfig.With(TestConfig.Entry("a.mp4"));

        var item = Plan(folder, config)[0];

        Assert.AreEqual(UploadAction.Upload, item.Action);
        Assert.IsTrue(item.IsWork);
        StringAssert.Contains(item.Reason!, "new video");
        Assert.AreEqual(Path.GetFullPath(file), item.FullPath);
        Assert.AreEqual(7, item.FileSize);
        Assert.IsNull(item.ExistingVideoId);
        Assert.IsTrue(item.Extras.IsEmpty);
    }

    [TestMethod]
    public void A_missing_file_blocks_the_entry()
    {
        using var folder = new TempFolder();
        var config = TestConfig.With(TestConfig.Entry("missing.mp4"));

        var item = Plan(folder, config)[0];

        Assert.AreEqual(UploadAction.Blocked, item.Action);
        Assert.IsTrue(item.IsBlocked);
        Assert.IsFalse(item.IsWork);
        StringAssert.Contains(item.Reason!, "not found");
    }

    [TestMethod]
    public void An_entry_without_a_file_is_blocked()
    {
        using var folder = new TempFolder();
        var config = TestConfig.With(new VideoEntry { Title = "T" });

        var item = Plan(folder, config)[0];

        Assert.AreEqual(UploadAction.Blocked, item.Action);
        StringAssert.Contains(item.Reason!, "'file'");
    }

    [TestMethod]
    public void A_disabled_entry_is_skipped()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var config = TestConfig.With(new VideoEntry { File = "a.mp4", Title = "T", Enabled = false });

        var item = Plan(folder, config)[0];

        Assert.AreEqual(UploadAction.Skip, item.Action);
        StringAssert.Contains(item.Reason!, "disabled");
    }

    [TestMethod]
    public void An_invalid_configuration_blocks_the_entry_and_reports_the_issues()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var config = TestConfig.With(new VideoEntry { File = "a.mp4", Category = "Nope" });

        var item = Plan(folder, config)[0];

        Assert.AreEqual(UploadAction.Blocked, item.Action);
        Assert.IsTrue(item.Issues.Any(issue => issue.Setting is "title" or "category"));
    }

    [TestMethod]
    public void A_duplicate_entry_is_blocked()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var config = TestConfig.With(TestConfig.Entry("a.mp4"), TestConfig.Entry("./A.MP4"));

        var plan = Plan(folder, config);

        Assert.AreEqual(UploadAction.Upload, plan[0].Action);
        Assert.AreEqual(UploadAction.Blocked, plan[1].Action);
        StringAssert.Contains(plan[1].Reason!, "duplicate");
    }

    [TestMethod]
    public void Defaults_are_inherited_and_manual_work_is_reported()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        var defaults = new VideoSettings
        {
            Category = "Music",
            Description = "From defaults",
            Comments = CommentPolicy.Off,
            Playlists = ["Songs"],
        };

        var item = Plan(folder, TestConfig.WithDefaults(defaults, new VideoEntry { File = "a.mp4", Title = "T" }))[0];

        Assert.AreEqual("From defaults", item.Settings!.Description);
        Assert.AreEqual("Music", item.Settings.Category);
        CollectionAssert.AreEqual(new[] { "Songs" }, item.Playlists.ToArray());
        Assert.IsTrue(item.ManualFollowUps.Any(followUp => followUp.Contains("Comments: off")));
    }

    [TestMethod]
    public void A_thumbnail_next_to_the_video_is_part_of_the_upload()
    {
        using var folder = new TempFolder();
        var thumbnail = folder.WriteFile("a.jpg");
        folder.WriteFile("a.mp4");
        var config = TestConfig.With(TestConfig.Entry("a.mp4"));
        config.Videos[0].Thumbnail = "a.jpg";

        var item = Plan(folder, config)[0];

        Assert.AreEqual(thumbnail, item.Extras.ThumbnailPath);
        Assert.AreEqual(thumbnail, item.ThumbnailPath);
    }

    // --- decisions based on the recorded state ---------------------------------

    [TestMethod]
    public void A_finished_video_is_skipped()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");

        var item = PlanWithState(folder, State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1"));

        Assert.AreEqual(UploadAction.Skip, item.Action);
        StringAssert.Contains(item.Reason!, "already uploaded as vid1");
        Assert.AreEqual("vid1", item.ExistingVideoId);
    }

    [TestMethod]
    public void A_finished_video_whose_file_changed_is_blocked()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var stale = State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1");
        File.AppendAllText(folder["a.mp4"], "changed");

        var item = PlanWithState(folder, stale);

        Assert.AreEqual(UploadAction.Blocked, item.Action);
        StringAssert.Contains(item.Reason!, "--force");
    }

    [TestMethod]
    public void A_finished_video_without_a_video_id_is_blocked()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");

        var item = PlanWithState(folder, State(folder, "a.mp4", UploadStatus.Completed));

        Assert.AreEqual(UploadAction.Blocked, item.Action);
        StringAssert.Contains(item.Reason!, "video id");
    }

    [TestMethod]
    public void A_missing_thumbnail_of_a_finished_video_is_an_extra()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        folder.WriteFile("a.jpg");

        var item = PlanWithState(
            folder,
            State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1"),
            thumbnail: "a.jpg");

        Assert.AreEqual(UploadAction.ApplyExtras, item.Action);
        Assert.IsNotNull(item.Extras.ThumbnailPath);
        Assert.AreEqual("vid1", item.ExistingVideoId);
    }

    [TestMethod]
    public void A_missing_playlist_of_a_finished_video_is_an_extra()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var state = State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1");
        state.ThumbnailApplied = true;
        state.PlaylistsApplied.Add("Songs");

        var item = PlanWithState(folder, state, playlists: ["Songs", "Live"]);

        Assert.AreEqual(UploadAction.ApplyExtras, item.Action);
        CollectionAssert.AreEqual(new[] { "Live" }, item.Extras.Playlists.ToArray());
    }

    [TestMethod]
    public void A_finished_video_with_all_extras_is_skipped()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        folder.WriteFile("a.jpg");
        var state = State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1");
        state.ThumbnailApplied = true;
        state.PlaylistsApplied.Add("Songs");

        var item = PlanWithState(folder, state, thumbnail: "a.jpg", playlists: ["Songs"]);

        Assert.AreEqual(UploadAction.Skip, item.Action);
        Assert.IsTrue(item.Extras.IsEmpty);
    }

    [TestMethod]
    public void An_interrupted_upload_is_resumed()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var state = State(folder, "a.mp4", UploadStatus.Uploading);
        state.ResumableSessionUri = "https://upload.example/session";
        state.BytesSent = 3;

        var item = PlanWithState(folder, state);

        Assert.AreEqual(UploadAction.Resume, item.Action);
        Assert.AreEqual("https://upload.example/session", item.ResumableSessionUri);
        Assert.AreEqual(3, item.BytesSent);
        StringAssert.Contains(item.Reason!, "continuing the interrupted upload");
    }

    [TestMethod]
    public void An_interrupted_upload_whose_file_changed_starts_over()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var state = State(folder, "a.mp4", UploadStatus.Uploading);
        state.ResumableSessionUri = "https://upload.example/session";
        File.AppendAllText(folder["a.mp4"], "changed");

        var item = PlanWithState(folder, state);

        Assert.AreEqual(UploadAction.Upload, item.Action);
        Assert.IsNull(item.ResumableSessionUri);
    }

    [TestMethod]
    public void No_resume_restarts_an_interrupted_upload()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var state = State(folder, "a.mp4", UploadStatus.Uploading);
        state.ResumableSessionUri = "https://upload.example/session";

        var item = PlanWithState(folder, state, options: new UploadPlanOptions { Resume = false });

        Assert.AreEqual(UploadAction.Upload, item.Action);
    }

    [TestMethod]
    public void A_failed_upload_is_retried()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");
        var state = State(folder, "a.mp4", UploadStatus.Failed);
        state.LastError = "boom";

        var item = PlanWithState(folder, state);

        Assert.AreEqual(UploadAction.Upload, item.Action);
        StringAssert.Contains(item.Reason!, "boom");
    }

    [TestMethod]
    public void Force_uploads_a_finished_video_again()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4", "content");

        var item = PlanWithState(
            folder,
            State(folder, "a.mp4", UploadStatus.Completed, videoId: "vid1"),
            options: new UploadPlanOptions { Force = true });

        Assert.AreEqual(UploadAction.Upload, item.Action);
        StringAssert.Contains(item.Reason!, "--force");
    }

    [TestMethod]
    public void Only_filters_the_planned_videos()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("b.mp4");
        folder.WriteFile("sub/c.mp4");
        var config = TestConfig.With(TestConfig.Entry("a.mp4"), TestConfig.Entry("b.mp4"), TestConfig.Entry("sub/c.mp4"));

        var plan = Plan(folder, config, new UploadPlanOptions { Only = ["b*", "sub/*"] });

        Assert.AreEqual(UploadAction.Skip, plan[0].Action);
        StringAssert.Contains(plan[0].Reason!, "--only");
        Assert.AreEqual(UploadAction.Upload, plan[1].Action);
        Assert.AreEqual(UploadAction.Upload, plan[2].Action);
    }

    [TestMethod]
    [DataRow("a.mp4", "a.mp4", true)]
    [DataRow("a.mp4", "*.mp4", true)]
    [DataRow("a.mp4", "b*", false)]
    [DataRow("sub/a.mp4", "sub/*", true)]
    [DataRow("sub/a.mp4", "*.mp4", true)]
    [DataRow("sub/a.mp4", "other/*", false)]
    public void MatchesOnly_accepts_names_and_globs(string file, string filter, bool expected)
    {
        Assert.AreEqual(expected, UploadPlanner.MatchesOnly(file, [filter]));
    }

    [TestMethod]
    public void MatchesOnly_accepts_everything_without_filters()
    {
        Assert.IsTrue(UploadPlanner.MatchesOnly("a.mp4", []));
    }

    private static List<UploadPlanItem> Plan(
        TempFolder folder,
        UtuConfig config,
        UploadPlanOptions? options = null,
        UploadStateStore? state = null) =>
            [.. new UploadPlanner(state).CreatePlan(config, folder.Path, options).Items];

    private static UploadPlanItem PlanWithState(
        TempFolder folder,
        VideoUploadState state,
        string? thumbnail = null,
        IReadOnlyList<string>? playlists = null,
        UploadPlanOptions? options = null)
    {
        var entry = new VideoEntry { File = state.File, Title = "T", Thumbnail = thumbnail, Playlists = playlists?.ToList() };
        var config = TestConfig.With(entry);

        return Plan(folder, config, options, new UploadStateStore(folder[".utu/state.json"], Document(state)))[0];
    }

    private static VideoUploadState State(TempFolder folder, string file, UploadStatus status, string? videoId = null)
    {
        var identity = FileIdentity.FromFile(folder[file]);

        return new VideoUploadState
        {
            File = file,
            Status = status,
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
}
