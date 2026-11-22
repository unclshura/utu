using System.Globalization;

namespace Utu.Cli;

/// <summary>Turns command line arguments into a <see cref="CommandLine" />.</summary>
public static class CommandLineParser
{
    /// <summary>Exit code used for invalid command lines.</summary>
    public const int UsageExitCode = 2;

    /// <summary>Parses <paramref name="arguments" />.</summary>
    /// <exception cref="UtuException">The command line is invalid.</exception>
    public static CommandLine Parse(IEnumerable<string> arguments)
    {
        var tokens = arguments.Where(token => !string.IsNullOrWhiteSpace(token)).ToList();
        var line = new CommandLine();
        var commandSelected = false;
        var only = new List<string>();

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index].Trim();

            if (token is "-h" or "-?" or "--help" or "/?" or "/help")
            {
                line = line with { Command = UtuCommand.Help };
                commandSelected = true;
                continue;
            }

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var separator = token.IndexOf('=');
                var name = separator < 0 ? token[2..] : token[2..separator];
                var inlineValue = separator < 0 ? null : token[(separator + 1)..];

                string Value(string optionName)
                {
                    if (inlineValue is { Length: > 0 })
                    {
                        return inlineValue;
                    }

                    if (index + 1 >= tokens.Count)
                    {
                        throw new UtuException($"option '--{optionName}' needs a value", UsageExitCode);
                    }

                    return tokens[++index];
                }

                int PositiveNumber(string optionName)
                {
                    var text = Value(optionName);

                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
                        ? number
                        : throw new UtuException($"option '--{optionName}' needs a positive number, not '{text}'", UsageExitCode);
                }

                switch (name.ToLowerInvariant())
                {
                    case "config":
                        line = line with { ConfigFile = Value(name) };
                        break;
                    case "folder":
                    case "path":
                        line = line with { Folder = Value(name) };
                        break;
                    case "force":
                        line = line with { Force = true };
                        break;
                    case "dry-run":
                    case "dryrun":
                    case "whatif":
                        line = line with { DryRun = true };
                        break;
                    case "verbose":
                        line = line with { Verbose = true };
                        break;
                    case "no-color":
                    case "nocolor":
                        line = line with { NoColor = true };
                        break;
                    case "no-resume":
                    case "noresume":
                        line = line with { NoResume = true };
                        break;
                    case "recursive":
                        line = line with { Recursive = true };
                        break;
                    case "jobs":
                    case "parallel":
                        line = line with { Jobs = PositiveNumber(name) };
                        break;
                    case "retries":
                        line = line with { Retries = PositiveNumber(name) };
                        break;
                    case "only":
                        only.AddRange(Split(Value(name)));
                        break;
                    case "client-secrets":
                    case "secrets":
                        line = line with { ClientSecrets = Value(name) };
                        break;
                    case "category":
                        line = line with { Category = Value(name) };
                        break;
                    default:
                        throw new UtuException($"unknown option '--{name}'; run 'utu help'", UsageExitCode);
                }

                continue;
            }

            if (token.StartsWith('-') && token.Length > 1)
            {
                throw new UtuException($"unknown option '{token}'; run 'utu help'", UsageExitCode);
            }

            if (commandSelected)
            {
                throw new UtuException($"unexpected argument '{token}'; run 'utu help'", UsageExitCode);
            }

            line = line with { Command = ParseCommand(token) };
            commandSelected = true;
        }

        return line with { Only = [.. only] };
    }

    /// <summary>Parses a command name.</summary>
    /// <exception cref="UtuException">The command is unknown.</exception>
    public static UtuCommand ParseCommand(string name) => name.ToLowerInvariant() switch
    {
        "help" => UtuCommand.Help,
        "init" => UtuCommand.Init,
        "schedule" => UtuCommand.Schedule,
        // 'shedule' is the spelling the command was first asked for as; kept as
        // an alias so that the misspelling keeps working.
        "shedule" => UtuCommand.Schedule,
        "upload" => UtuCommand.Upload,
        _ => throw new UtuException($"unknown command '{name}'; run 'utu help'", UsageExitCode),
    };

    private static IEnumerable<string> Split(string value) =>
        value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}