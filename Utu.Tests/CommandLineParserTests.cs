using Utu;
using Utu.Cli;

namespace Utu.Tests;

[TestClass]
public sealed class CommandLineParserTests
{
    [TestMethod]
    public void No_arguments_selects_help()
    {
        var line = CommandLineParser.Parse([]);

        Assert.AreEqual(UtuCommand.Help, line.Command);
        Assert.AreEqual(".", line.Folder);
    }

    [TestMethod]
    public void Help_option_selects_help()
    {
        foreach (var token in new[] { "-h", "-?", "--help" })
        {
            Assert.AreEqual(UtuCommand.Help, CommandLineParser.Parse([token]).Command, token);
        }
    }

    [TestMethod]
    public void Parses_commands_case_insensitively()
    {
        Assert.AreEqual(UtuCommand.Init, CommandLineParser.Parse(["Init"]).Command);
        Assert.AreEqual(UtuCommand.Upload, CommandLineParser.Parse(["UPLOAD"]).Command);
    }

    [TestMethod]
    public void Parses_flags_with_equality_and_with_a_separate_value()
    {
        var withEquality = CommandLineParser.Parse(["upload", "--folder=C:\\videos", "--jobs=4", "--retries=5"]);
        var withSeparateValue = CommandLineParser.Parse(["upload", "--folder", "C:\\videos", "--jobs", "4", "--retries", "5"]);

        foreach (var line in new[] { withEquality, withSeparateValue })
        {
            Assert.AreEqual("C:\\videos", line.Folder);
            Assert.AreEqual(4, line.Jobs);
            Assert.AreEqual(5, line.Retries);
        }
    }

    [TestMethod]
    public void Parses_boolean_flags()
    {
        var line = CommandLineParser.Parse([
            "upload", "--force", "--dry-run", "--verbose", "--no-color", "--no-resume", "--recursive",
        ]);

        Assert.IsTrue(line.Force);
        Assert.IsTrue(line.DryRun);
        Assert.IsTrue(line.Verbose);
        Assert.IsTrue(line.NoColor);
        Assert.IsTrue(line.NoResume);
        Assert.IsTrue(line.Recursive);
    }

    [TestMethod]
    public void Splits_only_filters_on_commas_and_semicolons()
    {
        var line = CommandLineParser.Parse(["upload", "--only=*.mp4, second/* ;third.mp4"]);

        CollectionAssert.AreEqual(new[] { "*.mp4", "second/*", "third.mp4" }, line.Only.ToArray());
    }

    [TestMethod]
    public void Reads_the_remaining_options()
    {
        var line = CommandLineParser.Parse([
            "init", "--config=other.json", "--client-secrets=C:\\secrets.json", "--category=Music",
        ]);

        Assert.AreEqual("other.json", line.ConfigFile);
        Assert.AreEqual("C:\\secrets.json", line.ClientSecrets);
        Assert.AreEqual("Music", line.Category);
    }

    [TestMethod]
    public void Unknown_option_fails_with_the_usage_exit_code()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CommandLineParser.Parse(["upload", "--bogus"]));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "--bogus");
    }

    [TestMethod]
    public void Unknown_command_fails_with_the_usage_exit_code()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CommandLineParser.Parse(["frobnicate"]));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "frobnicate");
    }

    [TestMethod]
    public void Second_command_is_rejected()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CommandLineParser.Parse(["upload", "init"]));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
    }

    [TestMethod]
    [DataRow("--jobs=0")]
    [DataRow("--jobs=-1")]
    [DataRow("--jobs=many")]
    [DataRow("--retries=0")]
    public void Invalid_numbers_are_rejected(string option)
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CommandLineParser.Parse(["upload", option]));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
    }

    [TestMethod]
    public void Option_without_a_value_is_rejected()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CommandLineParser.Parse(["upload", "--folder"]));

        Assert.AreEqual(CommandLineParser.UsageExitCode, exception.ExitCode);
        StringAssert.Contains(exception.Message, "--folder");
    }

    [TestMethod]
    public void ParseCommand_maps_the_known_commands()
    {
        Assert.AreEqual(UtuCommand.Init, CommandLineParser.ParseCommand("init"));
        Assert.AreEqual(UtuCommand.Upload, CommandLineParser.ParseCommand("upload"));
        Assert.AreEqual(UtuCommand.Help, CommandLineParser.ParseCommand("help"));
    }
}
