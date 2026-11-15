using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class CategoryRegistryTests
{
    [TestMethod]
    [DataRow("Music", "10")]
    [DataRow("music", "10")]
    [DataRow("MUSIC", "10")]
    [DataRow("  Music  ", "10")]
    [DataRow("10", "10")]
    [DataRow("22", "22")]
    [DataRow(" 22 ", "22")]
    [DataRow("Gaming", "20")]
    [DataRow("Film & Animation", "1")]
    public void Resolves_names_and_ids(string value, string expected)
    {
        Assert.AreEqual(expected, CategoryRegistry.Resolve(value));
    }

    [TestMethod]
    [DataRow("Nope")]
    [DataRow("9999")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("-1")]
    public void Returns_null_for_unknown_values(string value)
    {
        Assert.IsNull(CategoryRegistry.Resolve(value));
    }

    [TestMethod]
    public void Returns_null_for_a_missing_value()
    {
        Assert.IsNull(CategoryRegistry.Resolve(null));
    }

    [TestMethod]
    public void Falls_back_to_music()
    {
        Assert.AreEqual(CategoryRegistry.Music, CategoryRegistry.ResolveOrDefault(null));
        Assert.AreEqual(CategoryRegistry.Music, CategoryRegistry.ResolveOrDefault("Nope"));
        Assert.AreEqual("20", CategoryRegistry.ResolveOrDefault("Gaming"));
    }

    [TestMethod]
    public void TryParseId_normalizes_numeric_ids()
    {
        Assert.IsTrue(CategoryRegistry.TryParseId("010", out var id));
        Assert.AreEqual("10", id);

        Assert.IsFalse(CategoryRegistry.TryParseId("Music", out _));
        Assert.IsFalse(CategoryRegistry.TryParseId("9999", out _));
        Assert.IsFalse(CategoryRegistry.TryParseId(null, out _));
    }

    [TestMethod]
    public void Names_are_described_for_error_messages()
    {
        Assert.IsTrue(CategoryRegistry.Names.Contains("Music"));
        StringAssert.Contains(CategoryRegistry.DescribeKnownNames(), "Music");
    }
}
