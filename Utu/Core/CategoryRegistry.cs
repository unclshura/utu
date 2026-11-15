using System.Globalization;

namespace Utu.Core;

/// <summary>
/// Maps YouTube video category names to the numeric ids used by
/// <c>snippet.categoryId</c>.
/// </summary>
public static class CategoryRegistry
{
    /// <summary>Category id of "Music", the utu default.</summary>
    public const string Music = "10";

    private static readonly Dictionary<string, string> ByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Film & Animation"] = "1",
            ["Film and Animation"] = "1",
            ["Autos & Vehicles"] = "2",
            ["Autos and Vehicles"] = "2",
            ["Music"] = Music,
            ["Pets & Animals"] = "15",
            ["Pets and Animals"] = "15",
            ["Sports"] = "17",
            ["Short Movies"] = "18",
            ["Travel & Events"] = "19",
            ["Travel and Events"] = "19",
            ["Gaming"] = "20",
            ["Videoblogging"] = "21",
            ["People & Blogs"] = "22",
            ["People and Blogs"] = "22",
            ["Comedy"] = "23",
            ["Entertainment"] = "24",
            ["News & Politics"] = "25",
            ["News and Politics"] = "25",
            ["Howto & Style"] = "26",
            ["Howto and Style"] = "26",
            ["Education"] = "27",
            ["Science & Technology"] = "28",
            ["Science and Technology"] = "28",
            ["Nonprofits & Activism"] = "29",
            ["Nonprofits and Activism"] = "29",
            ["Movies"] = "30",
            ["Anime/Animation"] = "31",
            ["Action/Adventure"] = "32",
            ["Classics"] = "33",
            ["Comedy (Movies)"] = "34",
            ["Documentary"] = "35",
            ["Drama"] = "36",
            ["Family"] = "37",
            ["Foreign"] = "38",
            ["Horror"] = "39",
            ["Sci-Fi/Fantasy"] = "40",
            ["Thriller"] = "41",
            ["Shorts"] = "42",
            ["Shows"] = "43",
            ["Trailers"] = "44",
        };

    /// <summary>All known category names, sorted, for help output.</summary>
    public static IReadOnlyCollection<string> Names => ByName.Keys;

    /// <summary>
    /// Resolves a category name or numeric id to a YouTube category id.
    /// </summary>
    /// <returns><see langword="null" /> when the value is empty or unknown.</returns>
    public static string? Resolve(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        var value = category.Trim();
        if (value.All(char.IsAsciiDigit))
        {
            return value.Length <= 3 ? value : null;
        }

        return ByName.TryGetValue(value, out var id) ? id : null;
    }

    /// <summary>Resolves a category the same way as <see cref="Resolve"/>, falling back to Music.</summary>
    public static string ResolveOrDefault(string? category) => Resolve(category) ?? Music;

    /// <summary>Describes the known categories for error messages.</summary>
    public static string DescribeKnownNames() =>
        string.Join(", ", ByName.Keys.Order(StringComparer.OrdinalIgnoreCase));

    /// <summary>Parses a numeric category id, ignoring culture specific digits.</summary>
    public static bool TryParseId(string? value, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is 0 or > 3 || !trimmed.All(char.IsAsciiDigit))
        {
            return false;
        }

        id = int.Parse(trimmed, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
