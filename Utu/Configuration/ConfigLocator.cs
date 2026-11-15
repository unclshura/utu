namespace Utu.Configuration;

/// <summary>Locates the global configuration in the home folder of the user.</summary>
public static class ConfigLocator
{
    /// <summary>Folder below the home folder that holds the global configuration.</summary>
    public const string FolderName = ".utu";

    /// <summary>Environment variable that replaces the location of the global configuration.</summary>
    public const string HomeVariable = "UTU_HOME";

    /// <summary>Home folder of the current user.</summary>
    public static string HomeFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home
            ? home
            : Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();

    /// <summary>
    /// Folder of the global configuration: <see cref="HomeVariable" /> when it is
    /// set, otherwise <c>&lt;home&gt;/.utu</c>.
    /// </summary>
    public static string GlobalFolder => GlobalFolderFrom(HomeFolder, Environment.GetEnvironmentVariable(HomeVariable));

    /// <summary>Path of the global configuration file.</summary>
    public static string GlobalConfigFile => Path.Combine(GlobalFolder, ConfigIO.DefaultFileName);

    /// <summary>Computes the folder of the global configuration without reading the environment.</summary>
    /// <param name="home">Home folder of the user.</param>
    /// <param name="overrideFolder">Value of <see cref="HomeVariable" />, when set.</param>
    public static string GlobalFolderFrom(string home, string? overrideFolder) =>
        Path.GetFullPath(
            string.IsNullOrWhiteSpace(overrideFolder)
                ? Path.Combine(home, FolderName)
                : overrideFolder);
}