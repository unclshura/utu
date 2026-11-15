using Utu;
using Utu.Cli;

// Ctrl+C cancels the current run; a second Ctrl+C ends the process immediately.

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    if (cancellation.IsCancellationRequested)
    {
        return;
    }

    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var line = new CommandLine();

try
{
    line = CommandLineParser.Parse(args);
    return await UtuApp.RunAsync(line, cancellation.Token);
}
catch (UtuException exception)
{
    // Errors caused by input are reported without a stack trace.
    new ConsoleUi(line.NoColor, line.Verbose).Error(exception.Message);

    if (exception.ExitCode == CommandLineParser.UsageExitCode)
    {
        Console.Error.WriteLine("Run 'utu help' for the available commands and options.");
    }

    return exception.ExitCode;
}
catch (OperationCanceledException)
{
    new ConsoleUi(line.NoColor, line.Verbose).Warning("cancelled");
    return 130;
}
catch (Exception exception)
{
    // Google API failures carry the API response in their message.
    new ConsoleUi(line.NoColor, line.Verbose).Error(exception.Message);

    if (args.Any(argument => argument.StartsWith("--verbose", StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine(exception);
    }

    return 1;
}
