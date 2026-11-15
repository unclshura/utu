using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class UploadStateStoreTests
{
    [TestMethod]
    public void A_missing_state_file_is_not_a_problem()
    {
        using var folder = new TempFolder();
        var path = folder[".utu/state.json"];

        var loaded = UploadStateStore.Load(path);

        Assert.IsNull(loaded.Warning);
        Assert.AreEqual(0, loaded.Document.Videos.Count);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void A_corrupt_state_file_is_preserved_and_reported()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile(".utu/state.json", "{ this is not json");

        var loaded = UploadStateStore.Load(path);

        Assert.IsNotNull(loaded.Warning);
        Assert.AreEqual(0, loaded.Document.Videos.Count);
        Assert.IsFalse(File.Exists(path));
        Assert.IsTrue(File.Exists(path + ".corrupt"));
        Assert.AreEqual("{ this is not json", File.ReadAllText(path + ".corrupt"));
    }

    [TestMethod]
    [DataRow("a.mp4", "a.mp4")]
    [DataRow("./a.mp4", "a.mp4")]
    [DataRow(".\\a.mp4", "a.mp4")]
    [DataRow("sub\\a.mp4", "sub/a.mp4")]
    [DataRow(" sub/a.mp4 ", "sub/a.mp4")]
    public void Keys_are_normalized(string value, string expected)
    {
        Assert.AreEqual(expected, UploadStateStore.NormalizeKey(value));
    }

    [TestMethod]
    public void Finds_a_record_through_a_different_spelling_of_the_path()
    {
        var store = new UploadStateStore("state.json");
        store.Update("sub\\a.mp4", state => state.VideoId = "vid1");

        Assert.AreEqual("vid1", store.Find("sub/a.mp4")!.VideoId);
        Assert.AreEqual("vid1", store.Find("./sub/a.mp4")!.VideoId);
        Assert.IsNull(store.Find("other.mp4"));
    }

    [TestMethod]
    public void GetOrCreate_returns_the_same_record()
    {
        var store = new UploadStateStore("state.json");

        var first = store.GetOrCreate("a.mp4");
        var second = store.GetOrCreate("./a.mp4");

        Assert.AreSame(first, second);
        Assert.AreEqual(UploadStatus.Pending, first.Status);
    }

    [TestMethod]
    public void Update_persists_the_state_when_asked()
    {
        using var folder = new TempFolder();
        var path = folder[".utu/state.json"];
        var store = new UploadStateStore(path);

        store.Update("a.mp4", state => state.Status = UploadStatus.Completed, persist: false);
        Assert.IsFalse(File.Exists(path));

        store.Update("a.mp4", state => state.VideoId = "vid1", persist: true);
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual("vid1", store.Find("a.mp4")!.VideoId);
        Assert.AreNotEqual(default, store.Find("a.mp4")!.UpdatedAt);

        var reloaded = UploadStateStore.Load(path).Document;
        Assert.AreEqual("vid1", reloaded.Videos["a.mp4"].VideoId);
        Assert.AreEqual(UploadStatus.Completed, reloaded.Videos["a.mp4"].Status);
    }

    [TestMethod]
    public void Load_normalizes_the_keys_of_an_existing_file()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile(
            ".utu/state.json",
            """
            {
              "version": 1,
              "videos": {
                ".\\sub\\a.mp4": { "file": "sub\\a.mp4", "status": "completed", "videoId": "vid1" }
              }
            }
            """);

        var loaded = UploadStateStore.Load(path);
        var store = new UploadStateStore(path, loaded.Document);

        Assert.AreEqual("vid1", store.Find("sub/a.mp4")!.VideoId);
        Assert.AreEqual(UploadStatus.Completed, loaded.Document.Videos["sub/a.mp4"].Status);
    }

    [TestMethod]
    public void Remove_forgets_a_video()
    {
        var store = new UploadStateStore("state.json");
        store.Update("a.mp4", state => state.VideoId = "vid1");

        store.Remove("a.mp4");

        Assert.IsNull(store.Find("a.mp4"));
    }

    [TestMethod]
    public void Save_creates_the_state_folder()
    {
        using var folder = new TempFolder();
        var path = folder[".utu/nested/state.json"];
        var store = new UploadStateStore(path);

        store.Save();

        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void A_record_matches_the_file_identity()
    {
        using var folder = new TempFolder();
        var file = folder.WriteFile("a.mp4", "content");
        var identity = FileIdentity.FromFile(file);

        var state = new VideoUploadState
        {
            FileSize = identity.Size,
            FileWriteTimeUtcTicks = identity.WriteTimeUtcTicks,
        };

        Assert.AreEqual(identity, state.Identity);
        Assert.IsTrue(state.Matches(identity));
        Assert.IsFalse(state.Matches(new FileIdentity(identity.Size + 1, identity.WriteTimeUtcTicks)));
    }
}
