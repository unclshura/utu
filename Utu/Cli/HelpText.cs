namespace Utu.Cli;

/// <summary>The help text of utu.</summary>
public static class HelpText
{
    /// <summary>One line description of utu.</summary>
    public const string Summary = "Upload videos to YouTube from a folder of .mp4 files.";

    /// <summary>The complete help text.</summary>
    public static string[] Lines =>
    [
        "Usage:",
        "  utu <command> [--option=<value>]",
        "",
        "Commands:",
        "  help      Show this help. This is also the default when no command is given.",
        "  init      Discover the .mp4 files of the folder and create utu.json with placeholders.",
        "  upload    Upload the .mp4 files according to utu.json.",
        "",
        "Options:",
        "  --config=<file>          Configuration file (default: utu.json).",
        "  --folder=<folder>        Folder that paths are resolved against (default: .).",
        "  --force                  Upload again, even when the upload already completed.",
        "  --dry-run                Show the plan without uploading anything.",
        "  --only=<pattern>         Only the matching videos; repeat or separate with ','.",
        "  --jobs=<n>               Parallel uploads (default: channel.parallelUploads).",
        "  --retries=<n>            Attempts per operation (default: 3).",
        "  --no-resume              Restart interrupted uploads instead of continuing them.",
        "  --recursive               init: also search sub folders.",
        "  --category=<name>        init: category of the videos (default: Music).",
        "  --client-secrets=<file>  OAuth client secrets of a desktop client.",
        "  --verbose                Print every detail.",
        "  --no-color               Plain output without colours.",
        "  --help                   Show this help.",
        "",
        "Configuration:",
        "  utu.json holds the channel settings and one entry per video:",
        "",
        "    {",
        "      \"channel\": { \"name\": \"My channel\", \"clientSecretsFile\": \"client_secret.json\" },",
        "      \"defaults\": { \"category\": \"Music\", \"privacy\": \"private\" },",
        "      \"videos\": [",
        "        { \"file\": \"video.mp4\", \"title\": \"Video\", \"thumbnail\": \"video.jpg\" }",
        "      ]",
        "    }",
        "",
        "Authentication:",
        "  The first upload opens a browser. The token is cached in .utu/auth, so later",
        "  runs do not ask again. utu never stores credentials of its own.",
    ];
}