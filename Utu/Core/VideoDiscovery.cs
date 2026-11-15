namespace Utu.Core;

/// <summary>Finds the video files and thumbnails that belong to a working folder.</summary>
public static class VideoDiscovery
{
    /// <summary>Extensions treated as uploadable video files.</summary>
    public static readonly string[] VideoExtensions = [".mp4"];

    /// <summary>Extensions treated as thumbnails, in order of preference.</summary>
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Returns <see langword="true" /> when the path has a video extension.</summary>
    public static bool IsVideoFile(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns <see langword="true" /> when the path has a thumbnail extension.</summary>
    public static bool IsImageFile(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the video files of <paramref name="folder"/> as paths relative to
    /// that folder, ordered by name.
    /// </summary>
    public static IReadOnlyList<string> FindVideos(string folder, bool recursive = false)
    {
        if (!Directory.Exists(folder))
        {
            throw new UtuException($"Folder not found: {folder}");
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        return
        [
            .. Directory
                .EnumerateFiles(folder, "*", option)
                .Where(IsVideoFile)
                .Select(path => Path.GetRelativePath(folder, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Returns the thumbnail that sits next to <paramref name="videoPath"/> and
    /// shares its name, or <see langword="null" /> when there is none.
    /// </summary>
    public static string? FindThumbnail(string videoPath)
    {
        var withoutExtension = Path.ChangeExtension(videoPath, null);

        foreach (var extension in ImageExtensions)
        {
            var candidate = withoutExtension + extension;
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
