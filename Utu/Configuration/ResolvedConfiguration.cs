namespace Utu.Configuration;

/// <summary>
/// The configuration of a run after the layers have been merged.
/// </summary>
/// <remarks>
/// Settings are layered, the first source that defines a value wins:
/// <list type="number">
///   <item>the video entry of the folder configuration;</item>
///   <item>the <c>defaults</c> of the folder configuration (<c>utu.json</c>);</item>
///   <item>the <c>defaults</c> and <c>channel</c> of the global configuration
///     (<c>%USERPROFILE%\.utu\utu.json</c>);</item>
///   <item>the values of <see cref="VideoSettings" />, <see cref="ChannelSettings" />
///     and the metadata factory.</item>
/// </list>
/// </remarks>
public sealed record ResolvedConfiguration
{
    /// <summary>Folder that relative paths of the folder configuration use.</summary>
    public string WorkingFolder { get; init; } = ".";

    /// <summary>Merged channel settings; every path is absolute.</summary>
    public ChannelSettings Channel { get; init; } = new();

    /// <summary>Folder defaults merged over the global defaults.</summary>
    public VideoSettings Defaults { get; init; } = new();

    /// <summary>The videos of the folder configuration.</summary>
    public IReadOnlyList<VideoEntry> Videos { get; init; } = [];

    /// <summary>Whether a global configuration was merged in.</summary>
    public bool HasGlobal { get; init; }

    /// <summary>Merges a folder configuration over an optional global configuration.</summary>
    /// <param name="workingFolder">Folder of <paramref name="folder" />.</param>
    /// <param name="folder">The folder configuration, or <see langword="null" />.</param>
    /// <param name="global">The global configuration, or <see langword="null" />.</param>
    /// <param name="globalFolder">Folder of <paramref name="global" />.</param>
    public static ResolvedConfiguration Resolve(
        string workingFolder,
        UtuConfig? folder,
        UtuConfig? global = null,
        string? globalFolder = null)
    {
        var root = Path.GetFullPath(workingFolder);
        var globalRoot = globalFolder is null ? root : Path.GetFullPath(globalFolder);

        return new ResolvedConfiguration
        {
            WorkingFolder = root,
            Channel = ChannelSettings.Resolve(folder?.Channel, root, global?.Channel, globalRoot),
            Defaults = (folder?.Defaults ?? new VideoSettings()).WithFallback(
                global?.Defaults is { } globalDefaults
                    ? ResolveRelativePaths(globalDefaults, globalRoot)
                    : null),
            Videos = folder?.Videos ?? [],
            HasGlobal = global is not null,
        };
    }

    private static VideoSettings ResolveRelativePaths(VideoSettings settings, string folder)
    {
        var copy = settings.Clone();

        if (!string.IsNullOrWhiteSpace(copy.Thumbnail) && !Path.IsPathRooted(copy.Thumbnail))
        {
            copy.Thumbnail = Core.PathResolver.Resolve(folder, copy.Thumbnail);
        }

        return copy;
    }
}