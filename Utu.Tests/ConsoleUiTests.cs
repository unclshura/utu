using Spectre.Console;
using Utu.Cli;

namespace Utu.Tests;

[TestClass]
public sealed class ConsoleUiTests
{
    [TestMethod]
    [DataRow(40)]
    [DataRow(80)]
    [DataRow(120)]
    public void Tables_are_one_cell_narrower_than_the_console(int width)
    {
        var (ui, output) = CreateUi(width);

        ui.Write(WideTable());

        var lines = Lines(output());
        Assert.AreEqual(width - 1, lines.Max(line => line.Length), Render(lines));
    }

    [TestMethod]
    public void Tables_fit_a_narrow_console()
    {
        var (ui, output) = CreateUi(20);

        ui.Write(WideTable());

        var lines = Lines(output());
        Assert.IsTrue(lines.Length > 0);
        Assert.IsTrue(lines.All(line => line.Length <= 19), Render(lines));
    }

    [TestMethod]
    public void Messages_are_written_to_the_injected_console()
    {
        var (ui, output) = CreateUi(80);

        ui.Plain("plain");
        ui.Markup("[red]warned[/]");

        var lines = Lines(output());
        CollectionAssert.AreEqual(new[] { "plain", "warned" }, lines);
    }

    private static (ConsoleUi Ui, Func<string> Output) CreateUi(int width)
    {
        var writer = new StringWriter();

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });

        console.Profile.Width = width;

        return (new ConsoleUi(noColor: true, console: console), () => writer.ToString());
    }

    private static Table WideTable() => new Table()
        .AddColumn("Video")
        .AddColumn("Details")
        .AddRow(new Text("video.mp4"), new Text(new string('x', 200)));

    private static string[] Lines(string output) => output
        .Split('\n')
        .Select(line => line.TrimEnd('\r'))
        .Where(line => line.Length > 0)
        .ToArray();

    private static string Render(IEnumerable<string> lines) => string.Join(Environment.NewLine, lines);
}