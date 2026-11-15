namespace Utu.Core;

/// <summary>Resolves the paths of a configuration against the working folder.</summary>
public static class PathResolver
{
    /// <summary>
    /// Resolves a configuration path. Absolute paths are returned unchanged,
    /// relative ones are combined with <paramref name="workingFolder" />.
    /// </summary>
    public static string Resolve(string workingFolder, string path) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workingFolder, path));

    /// <summary>Resolves a path, returning <see langword="null" /> for empty input.</summary>
    public static string? ResolveOptional(string workingFolder, string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Resolve(workingFolder, path);

    /// <summary>
    /// Returns <paramref name="path" /> relative to <paramref name="folder" />
    /// with forward slashes, so that configuration files are portable.
    /// </summary>
    public static string ToRelative(string folder, string path) =>
        Path.GetRelativePath(Path.GetFullPath(folder), Path.GetFullPath(path)).Replace('\\', '/');

    /// <summary>Normalizes a configured path for comparisons.</summary>
    public static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }
}