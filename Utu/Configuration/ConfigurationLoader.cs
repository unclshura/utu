using Utu.Core;

namespace Utu.Configuration;

/// <summary>A folder configuration merged with the global configuration.</summary>
public sealed class LoadedConfiguration
{
    /// <summary>Absolute path of the folder configuration that was read.</summary>
    public required string ConfigPath { get; init; }

    /// <summary>Absolute path of the global configuration, when one was read.</summary>
    public string? GlobalPath { get; init; }

    /// <summary>Absolute path of the folder of the global configuration.</summary>
    public string? GlobalFolder { get; init; }

    /// <summary>The merged configuration.</summary>
    public required ResolvedConfiguration Resolved { get; init; }
}

/// <summary>Reads the configuration layers of a run.</summary>
public static class ConfigurationLoader
{
    /// <summary>Absolute path of the global configuration file for the current user.</summary>
    public static string GlobalConfigPath(string? homeFolder = null, string? overrideFolder = null) =>
        Path.Combine(
            ConfigLocator.GlobalFolderFrom(
                homeFolder ?? ConfigLocator.HomeFolder,
                overrideFolder ?? Environment.GetEnvironmentVariable(ConfigLocator.HomeVariable)),
            ConfigIO.DefaultFileName);

    /// <summary>
    /// Reads the folder configuration and merges it over the global
    /// configuration of the current user.
    /// </summary>
    /// <param name="workingFolder">Folder of the configuration.</param>
    /// <param name="configFile">
    /// Value of <c>--config</c>; when it is set, no global configuration is used
    /// because the user named an exact file.
    /// </param>
    /// <param name="allowMissingFolderConfig">
    /// Whether a missing folder configuration is treated as empty, as
    /// <c>utu init</c> needs.
    /// </param>
    /// <param name="homeFolder">Replaces the home folder of the user; for tests.</param>
    /// <param name="overrideFolder">Replaces <see cref="ConfigLocator.HomeVariable" />; for tests.</param>
    /// <exception cref="UtuException">A configuration file is missing or invalid.</exception>
    public static LoadedConfiguration Load(
        string workingFolder,
        string? configFile = null,
        bool allowMissingFolderConfig = false,
        string? homeFolder = null,
        string? overrideFolder = null)
    {
        var root = Path.GetFullPath(workingFolder);
        var configPath = PathResolver.Resolve(root, configFile ?? ConfigIO.DefaultFileName);

        UtuConfig folder;
        if (File.Exists(configPath))
        {
            folder = ConfigIO.Load(configPath);
        }
        else if (allowMissingFolderConfig)
        {
            folder = new UtuConfig();
        }
        else
        {
            throw new UtuException($"'{configPath}' does not exist; run 'utu init' first");
        }

        var globalPath = FindGlobalConfig(configPath, configFile, homeFolder, overrideFolder);
        var global = globalPath is null ? null : ConfigIO.Load(globalPath);
        var globalFolder = globalPath is null ? null : Path.GetDirectoryName(globalPath);

        return new LoadedConfiguration
        {
            ConfigPath = configPath,
            GlobalPath = globalPath,
            GlobalFolder = globalFolder,
            Resolved = ResolvedConfiguration.Resolve(root, folder, global, globalFolder),
        };
    }

    /// <summary>
    /// Returns the global configuration file to merge in, or <see langword="null" />
    /// when there is none.
    /// </summary>
    /// <param name="configPath">Absolute path of the folder configuration.</param>
    /// <param name="configFile">Value of <c>--config</c>, when it was given.</param>
    /// <param name="homeFolder">Replaces the home folder of the user; for tests.</param>
    /// <param name="overrideFolder">Replaces <see cref="ConfigLocator.HomeVariable" />; for tests.</param>
    public static string? FindGlobalConfig(
        string configPath,
        string? configFile = null,
        string? homeFolder = null,
        string? overrideFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(configFile))
        {
            return null;
        }

        var globalPath = GlobalConfigPath(homeFolder, overrideFolder);

        if (string.Equals(Path.GetFullPath(globalPath), Path.GetFullPath(configPath), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(globalPath) ? globalPath : null;
    }
}
