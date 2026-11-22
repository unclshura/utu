using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Utu.Configuration;

/// <summary>Reads and writes <c>utu.json</c> and the state file.</summary>
public static class ConfigIO
{
    public const string DefaultFileName = "utu.json";

    /// <summary>Serializer options shared by the configuration and the state file.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    /// <summary>
    /// Serializer options that keep unset values, so that a template can name a
    /// setting with a placeholder instead of dropping it.
    /// </summary>
    public static JsonSerializerOptions PlaceholderOptions { get; } = new(Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Parses a configuration document.</summary>
    /// <exception cref="UtuException">The document is not valid JSON or has unknown members.</exception>
    public static UtuConfig Parse(string json)
    {
        UtuConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<UtuConfig>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new UtuException($"Invalid configuration: {exception.Message}", 2, exception);
        }

        return config ?? throw new UtuException("Invalid configuration: document is empty.", 2);
    }

    /// <summary>Reads a configuration file.</summary>
    /// <exception cref="UtuException">The file is missing or invalid.</exception>
    public static UtuConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new UtuException($"Configuration file not found: {path}");
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (IOException exception)
        {
            throw new UtuException($"Cannot read {path}: {exception.Message}", 1, exception);
        }
    }

    /// <summary>Reads a configuration file, returning <see langword="null" /> when it does not exist.</summary>
    /// <exception cref="UtuException">The file is invalid.</exception>
    public static UtuConfig? LoadOptional(string path) => File.Exists(path) ? Load(path) : null;

    /// <summary>Serializes a configuration document.</summary>
    public static string Serialize(UtuConfig config) =>
        JsonSerializer.Serialize(config, Options);

    /// <summary>
    /// Serializes a folder configuration whose <c>defaults</c> name every setting
    /// utu understands, using <see langword="null" /> for the values that are not
    /// set. Video entries keep only the values they define.
    /// </summary>
    public static string SerializeWithPlaceholders(UtuConfig config)
    {
        if (JsonSerializer.SerializeToNode(config, PlaceholderOptions) is not JsonObject document)
        {
            return JsonSerializer.Serialize(config, PlaceholderOptions);
        }

        if (document["channel"] is JsonObject channel)
        {
            RemoveUnset(channel);
        }

        if (document["videos"] is JsonArray videos)
        {
            foreach (var video in videos.OfType<JsonObject>())
            {
                RemoveUnset(video);
            }
        }

        return document.ToJsonString(PlaceholderOptions);
    }

    /// <summary>Writes a configuration document.</summary>
    public static void Save(string path, UtuConfig config) => Write(path, Serialize(config));

    /// <summary>Writes a folder configuration with placeholders for unset settings.</summary>
    public static void SaveWithPlaceholders(string path, UtuConfig config) =>
        Write(path, SerializeWithPlaceholders(config));

    private static void RemoveUnset(JsonObject element)
    {
        foreach (var name in element
            .Where(property => property.Value is null)
            .Select(property => property.Key)
            .ToList())
        {
            element.Remove(name);
        }
    }

    private static void Write(string path, string json)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json + Environment.NewLine);
    }
}
