using System.Text.RegularExpressions;

namespace Utu.Core;

/// <summary>Validation and normalization of BCP-47 language codes.</summary>
public static partial class LanguageCodes
{
    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex CodePattern();

    /// <summary>Returns <see langword="true" /> when the value looks like a language code.</summary>
    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CodePattern().IsMatch(code.Trim());

    /// <summary>
    /// Normalizes a language code to <c>aa-BB</c> casing, or returns
    /// <see langword="null" /> when the value is empty or invalid.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (!IsValid(code))
        {
            return null;
        }

        var parts = code!.Trim().Split('-');
        var result = new List<string>(parts.Length) { parts[0].ToLowerInvariant() };

        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index];
            result.Add(part.Length == 2
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());
        }

        return string.Join('-', result);
    }
}
