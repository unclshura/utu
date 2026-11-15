namespace Utu.YouTubeApi;

/// <summary>Maps thumbnail file extensions to MIME types.</summary>
public static class ThumbnailMime
{
    public const string Default = "image/jpeg";

    /// <summary>Returns the content type of a thumbnail file.</summary>
    public static string FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => Default,
    };
}