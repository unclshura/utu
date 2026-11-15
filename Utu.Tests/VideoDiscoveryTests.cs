using Utu;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class VideoDiscoveryTests
{
    [TestMethod]
    public void Finds_only_videos_and_returns_relative_paths()
    {
        using var folder = new TempFolder();
        folder.WriteFile("b.mp4");
        folder.WriteFile("A.MP4");
        folder.WriteFile("notes.txt");
        folder.CreateDirectory("sub");
        folder.WriteFile("sub/c.mp4");

        var videos = VideoDiscovery.FindVideos(folder.Path);

        CollectionAssert.AreEqual(new[] { "A.MP4", "b.mp4" }, videos.ToArray());
    }

    [TestMethod]
    public void Finds_videos_in_sub_folders_when_recursive()
    {
        using var folder = new TempFolder();
        folder.WriteFile("a.mp4");
        folder.WriteFile("sub/deeper/b.mp4");

        var videos = VideoDiscovery.FindVideos(folder.Path, recursive: true);

        Assert.AreEqual(2, videos.Count);
        CollectionAssert.Contains(videos.Select(v => v.Replace('\\', '/')).ToArray(), "sub/deeper/b.mp4");
    }

    [TestMethod]
    public void Reports_a_missing_folder()
    {
        using var folder = new TempFolder();
        var missing = Path.Combine(folder.Path, "nope");

        var exception = Assert.ThrowsExactly<UtuException>(() => VideoDiscovery.FindVideos(missing));

        Assert.AreEqual(1, exception.ExitCode);
    }

    [TestMethod]
    [DataRow("a.mp4", true)]
    [DataRow("a.MP4", true)]
    [DataRow("a.mkv", false)]
    [DataRow("a.txt", false)]
    public void Recognizes_video_extensions(string path, bool expected)
    {
        Assert.AreEqual(expected, VideoDiscovery.IsVideoFile(path));
    }

    [TestMethod]
    [DataRow("a.jpg", true)]
    [DataRow("a.JPEG", true)]
    [DataRow("a.png", true)]
    [DataRow("a.webp", true)]
    [DataRow("a.mp4", false)]
    public void Recognizes_thumbnail_extensions(string path, bool expected)
    {
        Assert.AreEqual(expected, VideoDiscovery.IsImageFile(path));
    }

    [TestMethod]
    public void Finds_a_thumbnail_next_to_the_video()
    {
        using var folder = new TempFolder();
        var video = folder.WriteFile("song.mp4");
        var thumbnail = folder.WriteFile("song.jpg");
        folder.WriteFile("other.png");

        Assert.AreEqual(thumbnail, VideoDiscovery.FindThumbnail(video));
    }

    [TestMethod]
    public void Prefers_the_first_known_thumbnail_extension()
    {
        using var folder = new TempFolder();
        var video = folder.WriteFile("song.mp4");
        folder.WriteFile("song.png");
        var jpeg = folder.WriteFile("song.jpeg");

        Assert.AreEqual(jpeg, VideoDiscovery.FindThumbnail(video));
        Assert.AreNotEqual(folder["song.png"], VideoDiscovery.FindThumbnail(video));
    }

    [TestMethod]
    public void Reports_a_missing_thumbnail()
    {
        using var folder = new TempFolder();
        var video = folder.WriteFile("song.mp4");

        Assert.IsNull(VideoDiscovery.FindThumbnail(video));
    }
}
