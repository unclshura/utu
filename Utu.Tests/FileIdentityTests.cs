using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class FileIdentityTests
{
    [TestMethod]
    public void Matches_an_unchanged_file()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("a.mp4", "content");

        var identity = FileIdentity.FromFile(path);

        Assert.AreEqual(7, identity.Size);
        Assert.IsTrue(identity.Matches(path));
        StringAssert.Contains(identity.Describe(), "bytes");
    }

    [TestMethod]
    public void Detect_that_a_file_changed()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("a.mp4", "content");

        var identity = FileIdentity.FromFile(path);
        File.WriteAllText(path, "content that is longer");

        Assert.IsFalse(identity.Matches(path));
        Assert.AreNotEqual(identity, FileIdentity.FromFile(path));
    }

    [TestMethod]
    public void Does_not_match_a_missing_file()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("a.mp4");
        var identity = FileIdentity.FromFile(path);

        File.Delete(path);

        Assert.IsFalse(identity.Matches(path));
    }

    [TestMethod]
    public void Identity_is_made_of_size_and_write_time()
    {
        var identity = new FileIdentity(10, 20);

        Assert.AreEqual(new FileIdentity(10, 20), identity);
        Assert.AreNotEqual(new FileIdentity(11, 20), identity);
        Assert.AreNotEqual(new FileIdentity(10, 21), identity);
    }
}
