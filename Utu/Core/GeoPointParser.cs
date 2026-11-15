using System.Globalization;

namespace Utu.Core;

/// <summary>Parses the <c>location</c> setting into a latitude/longitude pair.</summary>
public static class GeoPointParser
{
    /// <summary>
    /// Parses <c>latitude,longitude</c> or <c>latitude,longitude,altitude</c>.
    /// Comma, semicolon and space separators are accepted.
    /// </summary>
    public static bool TryParse(
        string? value,
        out double latitude,
        out double longitude,
        out double? altitude)
    {
        latitude = 0;
        longitude = 0;
        altitude = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (!TryParseNumber(parts[0], out latitude) || !TryParseNumber(parts[1], out longitude))
        {
            latitude = 0;
            longitude = 0;
            return false;
        }

        if (parts.Length == 3)
        {
            if (!TryParseNumber(parts[2], out var parsedAltitude))
            {
                latitude = 0;
                longitude = 0;
                return false;
            }

            altitude = parsedAltitude;
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            latitude = 0;
            longitude = 0;
            altitude = null;
            return false;
        }

        return true;
    }

    private static bool TryParseNumber(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
        !double.IsNaN(result) &&
        !double.IsInfinity(result);
}
