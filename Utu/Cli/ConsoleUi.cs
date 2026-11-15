using Spectre.Console;

namespace Utu.Cli;

/// <summary>Colourful console output.</summary>
public sealed class ConsoleUi
{
    private readonly bool _color;
    private readonly bool _verbose;

    /// <param name="noColor">Disable colours and markup.</param>
    /// <param name="verbose">Print details.</param>
    public ConsoleUi(bool noColor = false, bool verbose = false)
    {
        _color = !noColor;
        _verbose = verbose;
    }

    /// <summary>Whether colours are used.</summary>
    public bool IsColor => _color;

    /// <summary>Whether details are printed.</summary>
    public bool IsVerbose => _verbose;

    /// <summary>Writes a line of markup; markup is stripped when colours are off.</summary>
    public void Markup(string markup)
    {
        if (_color)
        {
            AnsiConsole.MarkupLine(markup);
        }
        else
        {
            AnsiConsole.WriteLine(Spectre.Console.Markup.Remove(markup));
        }
    }

    /// <summary>Writes plain text.</summary>
    public void Plain(string text) => AnsiConsole.WriteLine(text);

    /// <summary>Writes an empty line.</summary>
    public void Blank() => AnsiConsole.WriteLine();

    /// <summary>Writes a heading.</summary>
    public void Heading(string text)
    {
        if (_color)
        {
            AnsiConsole.Write(new Rule($"[bold deepskyblue1]{Spectre.Console.Markup.Escape(text)}[/]").LeftJustified());
        }
        else
        {
            AnsiConsole.WriteLine(text);
            AnsiConsole.WriteLine(new string('-', Math.Min(Math.Max(text.Length, 3), 60)));
        }
    }

    /// <summary>Writes a neutral message.</summary>
    public void Info(string message) => Markup($"[grey]{Spectre.Console.Markup.Escape(message)}[/]");

    /// <summary>Writes a success message.</summary>
    public void Success(string message) => Markup($"[green]{Spectre.Console.Markup.Escape(message)}[/]");

    /// <summary>Writes a warning.</summary>
    public void Warning(string message) => Markup($"[yellow]warning:[/] {Spectre.Console.Markup.Escape(message)}");

    /// <summary>Writes an error.</summary>
    public void Error(string message) => Markup($"[red]error:[/] {Spectre.Console.Markup.Escape(message)}");

    /// <summary>Writes a hint about manual work.</summary>
    public void Manual(string message) => Markup($"[orange3]manual:[/] {Spectre.Console.Markup.Escape(message)}");

    /// <summary>Writes a detail, only in verbose mode.</summary>
    public void Detail(string message)
    {
        if (_verbose)
        {
            Markup($"[dim]{Spectre.Console.Markup.Escape(message)}[/]");
        }
    }

    /// <summary>Writes a table.</summary>
    public void Write(Table table) => AnsiConsole.Write(table);
}