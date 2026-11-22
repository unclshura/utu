namespace Utu.Configuration;

/// <summary>
/// Channel settings of a run. The optional values of the configuration layers
/// are filled in, and the paths are absolute.
/// </summary>
/// <remarks>
/// A folder configuration overrides the global configuration, which overrides
/// the values of this type. Relative paths are resolved against the folder of
/// the configuration file that defined them.
/// </remarks>
public sealed record ChannelSettings
{
    /// <summary>OAuth client secrets used when no configuration names a file.</summary>
    public const string DefaultClientSecretsFile = "client_secrets.json";

    /// <summary>Token cache folder used when no configuration names one.</summary>
    public const string DefaultTokenCacheDirectory = "auth";

    /// <summary>State file used when no configuration names one.</summary>
    public const string DefaultStateFile = ".utu/state.json";

    /// <summary>Name of the cached token used when no configuration names one.</summary>
    public const string DefaultUser = "default";

    /// <summary>Number of parallel uploads used when no configuration names one.</summary>
    public const int DefaultParallelUploads = 3;

    /// <summary>Informational name of the channel, shown in reports.</summary>
    public string? Name { get; init; }

    /// <summary>Absolute path of the OAuth client secrets.</summary>
    /// <remarks>
    /// This is the path the user stores the downloaded secrets at; utu reads
    /// the protected version of it. See <c>ClientSecretsFile</c>.
    /// </remarks>
    public string ClientSecretsFile { get; init; } = DefaultClientSecretsFile;

    /// <summary>Absolute path of the token cache folder.</summary>
    public string TokenCacheDirectory { get; init; } = DefaultTokenCacheDirectory;

    /// <summary>Absolute path of the state file.</summary>
    public string StateFile { get; init; } = DefaultStateFile;

    /// <summary>Name of the cached OAuth token.</summary>
    public string User { get; init; } = DefaultUser;

    /// <summary>Number of parallel uploads.</summary>
    public int ParallelUploads { get; init; } = DefaultParallelUploads;

    /// <summary>Whether failed or interrupted uploads are continued.</summary>
    public bool ResumeUploads { get; init; } = true;

    /// <summary>
    /// Merges <paramref name="folder" /> over <paramref name="global" /> and
    /// applies the values of this type to what neither defines.
    /// </summary>
    /// <param name="folder">Channel section of the folder configuration.</param>
    /// <param name="workingFolder">Folder of the folder configuration.</param>
    /// <param name="global">Channel section of the global configuration.</param>
    /// <param name="globalFolder">Folder of the global configuration.</param>
    /// <param name="homeFolder">
    /// The <c>.utu</c> folder of the user that built in paths are relative to.
    /// Defaults to <paramref name="globalFolder" />, then to
    /// <paramref name="workingFolder" />.
    /// </param>
    public static ChannelSettings Resolve(
        ChannelConfig? folder,
        string workingFolder,
        ChannelConfig? global = null,
        string? globalFolder = null,
        string? homeFolder = null)
    {
        var home = homeFolder ?? globalFolder ?? workingFolder;

        string PathSetting(
            string? local,
            string root,
            string? inherited,
            string inheritedRoot,
            string fallbackRoot,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(local))
            {
                return Core.PathResolver.Resolve(root, local);
            }

            return !string.IsNullOrWhiteSpace(inherited)
                ? Core.PathResolver.Resolve(inheritedRoot, inherited)
                : Core.PathResolver.Resolve(fallbackRoot, fallback);
        }

        return new ChannelSettings
        {
            Name = FirstSet(folder?.Name, global?.Name),
            ClientSecretsFile = PathSetting(
                folder?.ClientSecretsFile,
                workingFolder,
                global?.ClientSecretsFile,
                home,
                home,
                DefaultClientSecretsFile),
            TokenCacheDirectory = PathSetting(
                folder?.TokenCacheDirectory,
                workingFolder,
                global?.TokenCacheDirectory,
                home,
                home,
                DefaultTokenCacheDirectory),
            StateFile = PathSetting(
                folder?.StateFile,
                workingFolder,
                global?.StateFile,
                home,
                workingFolder,
                DefaultStateFile),
            User = FirstSet(folder?.User, global?.User) ?? DefaultUser,
            ParallelUploads = folder?.ParallelUploads ?? global?.ParallelUploads ?? DefaultParallelUploads,
            ResumeUploads = folder?.ResumeUploads ?? global?.ResumeUploads ?? true,
        };
    }

    private static string? FirstSet(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first
        : !string.IsNullOrWhiteSpace(second) ? second
        : null;
}