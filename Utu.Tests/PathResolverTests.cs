using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class PathResolverTests
{
    [TestMethod]
    public void Resolves_relative_paths_against_the_working_folder()
    {
        using var folder = new TempFolder();

        var resolved = PathResolver.Resolve(folder.Path, "sub/a.mp4");

        Assert.AreEqual(Path.GetFullPath(Path.Combine(folder.Path, "sub/a.mp4")), resolved);
    }

    [TestMethod]
    public void Keeps_absolute_paths()
    {
        using var folder = new TempFolder();
        var absolute = folder.WriteFile("a.mp4");

        Assert.AreEqual(Path.GetFullPath(absolute), PathResolver.Resolve(folder.Path, absolute));
    }

    [TestMethod]
    public void ResolveOptional_returns_null_for_empty_values()
    {
        using var folder = new TempFolder();

        Assert.IsNull(PathResolver.ResolveOptional(folder.Path, null));
        Assert.IsNull(PathResolver.ResolveOptional(folder.Path, "  "));
        Assert.IsNotNull(PathResolver.ResolveOptional(folder.Path, "a.mp4"));
    }

    [TestMethod]
    [DataRow("a/b.mp4", "a/b.mp4")]
    [DataRow("a\\b.mp4", "a/b.mp4")]
    [DataRow("./a/b.mp4", "a/b.mp4")]
    [DataRow(".\\a/b.mp4", "a/b.mp4")]
    [DataRow(" a/b.mp4 ", "a/b.mp4")]
    public void Normalize_makes_paths_comparable(string value, string expected)
    {
        Assert.AreEqual(expected, PathResolver.Normalize(value));
    }

    [TestMethod]
    public void ToRelative_uses_forward_slashes()
    {
        using var folder = new TempFolder();
        var file = folder.WriteFile("sub/a.mp4");

        Assert.AreEqual("sub/a.mp4", PathResolver.ToRelative(folder.Path, file));
    }
}
