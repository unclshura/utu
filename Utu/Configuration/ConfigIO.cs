using System.Text.Json;

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
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
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

    /// <summary>Serializes a configuration document.</summary>
    public static string Serialize(UtuConfig config) =>
        JsonSerializer.Serialize(config, Options);

    /// <summary>Writes a configuration document.</summary>
    public static void Save(string path, UtuConfig config)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serialize(config) + Environment.NewLine);
    }
}
