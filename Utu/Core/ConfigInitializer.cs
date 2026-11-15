using System.Text;
using Utu.Configuration;

namespace Utu.Core;

/// <summary>Settings of the <c>init</c> command.</summary>
public sealed record InitOptions
{
    /// <summary>Search sub directories too.</summary>
    public bool Recursive { get; init; }

    /// <summary>Category of the videos.</summary>
    public string Category { get; init; } = "Music";

    /// <summary>Replace an existing configuration instead of adding to it.</summary>
    public bool Force { get; init; }
}

/// <summary>A video that was added to the configuration.</summary>
/// <param name="File">Relative path of the video.</param>
/// <param name="Thumbnail">Relative path of the thumbnail, if one was found.</param>
public sealed record InitEntry(string File, string? Thumbnail);

/// <summary>Result of preparing a configuration.</summary>
/// <param name="Config">The configuration to write.</param>
/// <param name="Added">Videos that are new in the configuration.</param>
/// <param name="Kept">Videos that were already configured.</param>
/// <param name="MissingFiles">Configured videos that no longer exist.</param>
public sealed record InitResult(
    UtuConfig Config,
    IReadOnlyList<InitEntry> Added,
    IReadOnlyList<InitEntry> Kept,
    IReadOnlyList<string> MissingFiles)
{
    /// <summary>Whether the configuration has changed.</summary>
    public bool HasChanges => Added.Count > 0 || MissingFiles.Count > 0;
}

/// <summary>Builds a configuration file from the videos of a folder.</summary>
public static class ConfigInitializer
{
    /// <summary>
    /// Creates the configuration for <paramref name="folder" />. An existing
    /// configuration is extended, unless <see cref="InitOptions.Force" /> is set.
    /// </summary>
    public static InitResult Create(string folder, UtuConfig? existing, InitOptions? options = null)
    {
        options ??= new InitOptions();

        var root = Path.GetFullPath(folder);
        if (!Directory.Exists(root))
        {
            throw new UtuException($"folder '{root}' does not exist");
        }

        var discovered = VideoDiscovery.FindVideos(root, options.Recursive);

        var config = !options.Force && existing is not null ? existing : CreateEmpty(options);
        config.Videos ??= [];
        config.Defaults ??= CreateDefaults(options);

        var known = new HashSet<string>(
            config.Videos.Select(entry => PathResolver.Normalize(entry.File ?? string.Empty)),
            StringComparer.OrdinalIgnoreCase);

        var added = new List<InitEntry>();
        var kept = new List<InitEntry>();
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in discovered)
        {
            var file = PathResolver.Normalize(relative);
            existingFiles.Add(file);

            var thumbnail = VideoDiscovery.FindThumbnail(Path.Combine(root, relative));
            var entry = new InitEntry(file, thumbnail is null ? null : PathResolver.ToRelative(root, thumbnail));

            if (known.Add(file))
            {
                config.Videos.Add(new VideoEntry
                {
                    File = file,
                    Title = GuessTitle(file),
                    Thumbnail = entry.Thumbnail,
                });

                added.Add(entry);
            }
            else
            {
                kept.Add(entry);
            }
        }

        var missing = config.Videos
            .Select(entry => PathResolver.Normalize(entry.File ?? string.Empty))
            .Where(file => !existingFiles.Contains(file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        config.Videos.Sort((left, right) => string.Compare(left.File, right.File, StringComparison.OrdinalIgnoreCase));

        return new InitResult(config, added, kept, missing);
    }

    /// <summary>A readable title from a file name.</summary>
    public static string GuessTitle(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var builder = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            builder.Append(character is '_' or '.' or '-' ? ' ' : character);
        }

        var title = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return title.Length > VideoMetadataFactory.MaxTitleLength
            ? title[..VideoMetadataFactory.MaxTitleLength]
            : title;
    }

    private static UtuConfig CreateEmpty(InitOptions options) => new()
    {
        Defaults = CreateDefaults(options),
        Videos = [],
    };

    // Only the values init was told about are written; everything else is
    // inherited from the global configuration or from utu's built in defaults.
    private static VideoSettings CreateDefaults(InitOptions options) => new()
    {
        Category = options.Category,
    };
}
