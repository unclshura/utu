using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class GeoPointParserTests
{
    [TestMethod]
    public void Parses_latitude_and_longitude()
    {
        Assert.IsTrue(GeoPointParser.TryParse("48.8584,2.2945", out var latitude, out var longitude, out var altitude));

        Assert.AreEqual(48.8584, latitude, 0.0001);
        Assert.AreEqual(2.2945, longitude, 0.0001);
        Assert.IsNull(altitude);
    }

    [TestMethod]
    public void Parses_altitude_and_alternative_separators()
    {
        Assert.IsTrue(GeoPointParser.TryParse("48.8584; 2.2945 35", out _, out _, out var altitude));

        Assert.AreEqual(35, altitude!.Value, 0.0001);
    }

    [TestMethod]
    [DataRow("90,180")]
    [DataRow("-90,-180")]
    [DataRow("0,0")]
    public void Accepts_the_boundaries(string value)
    {
        Assert.IsTrue(GeoPointParser.TryParse(value, out _, out _, out _));
    }

    [TestMethod]
    [DataRow("91,0")]
    [DataRow("0,181")]
    [DataRow("-91,0")]
    [DataRow("abc,2")]
    [DataRow("1")]
    [DataRow("1,2,3,4")]
    [DataRow("")]
    [DataRow("   ")]
    public void Rejects_invalid_values(string value)
    {
        Assert.IsFalse(GeoPointParser.TryParse(value, out var latitude, out var longitude, out var altitude));
        Assert.AreEqual(0, latitude);
        Assert.AreEqual(0, longitude);
        Assert.IsNull(altitude);
    }

    [TestMethod]
    public void Rejects_a_missing_value()
    {
        Assert.IsFalse(GeoPointParser.TryParse(null, out _, out _, out _));
    }
}
