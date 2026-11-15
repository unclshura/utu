namespace Utu.Cli;

/// <summary>The commands of utu.</summary>
public enum UtuCommand
{
    /// <summary>Show the help text.</summary>
    Help,

    /// <summary>Create or extend <c>utu.json</c>.</summary>
    Init,

    /// <summary>Upload the videos of <c>utu.json</c>.</summary>
    Upload,
}

/// <summary>Parsed command line of a utu run.</summary>
public sealed record CommandLine
{
    /// <summary>Selected command.</summary>
    public UtuCommand Command { get; init; } = UtuCommand.Help;

    /// <summary>Working folder, relative paths are resolved against it.</summary>
    public string Folder { get; init; } = ".";

    /// <summary>Configuration file; <see langword="null" /> selects <c>utu.json</c>.</summary>
    public string? ConfigFile { get; init; }

    /// <summary>Upload or initialise everything again.</summary>
    public bool Force { get; init; }

    /// <summary>Show the plan but do not upload.</summary>
    public bool DryRun { get; init; }

    /// <summary>Print detailed output.</summary>
    public bool Verbose { get; init; }

    /// <summary>Do not use colours or markup.</summary>
    public bool NoColor { get; init; }

    /// <summary>Restart interrupted uploads instead of continuing them.</summary>
    public bool NoResume { get; init; }

    /// <summary>Search sub folders for videos.</summary>
    public bool Recursive { get; init; }

    /// <summary>Number of parallel uploads.</summary>
    public int? Jobs { get; init; }

    /// <summary>Attempts per operation.</summary>
    public int? Retries { get; init; }

    /// <summary>Only handle the videos that match these names or patterns.</summary>
    public IReadOnlyList<string> Only { get; init; } = [];

    /// <summary>OAuth client secrets file of a desktop client.</summary>
    public string? ClientSecrets { get; init; }

    /// <summary>Category of the videos, used by <see cref="UtuCommand.Init" />.</summary>
    public string? Category { get; init; }
}