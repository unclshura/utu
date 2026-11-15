using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class LanguageCodesTests
{
    [TestMethod]
    [DataRow("en")]
    [DataRow("en-US")]
    [DataRow("EN")]
    [DataRow("zh-Hans-CN")]
    [DataRow(" en-gb ")]
    public void Accepts_language_codes(string value)
    {
        Assert.IsTrue(LanguageCodes.IsValid(value));
    }

    [TestMethod]
    [DataRow("e")]
    [DataRow("en_US")]
    [DataRow("en-")]
    [DataRow("english")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Rejects_invalid_language_codes(string? value)
    {
        Assert.IsFalse(LanguageCodes.IsValid(value));
    }

    [TestMethod]
    [DataRow("EN-us", "en-US")]
    [DataRow("en", "en")]
    [DataRow("EN", "en")]
    [DataRow("zh-hans-cn", "zh-Hans-CN")]
    [DataRow("PT-br", "pt-BR")]
    public void Normalizes_casing(string value, string expected)
    {
        Assert.AreEqual(expected, LanguageCodes.Normalize(value));
    }

    [TestMethod]
    public void Normalize_returns_null_for_invalid_codes()
    {
        Assert.IsNull(LanguageCodes.Normalize(null));
        Assert.IsNull(LanguageCodes.Normalize("nonsense!"));
    }
}
