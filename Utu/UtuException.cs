namespace Utu;

/// <summary>
/// An error that is caused by user input or the environment and that should be
/// reported without a stack trace.
/// </summary>
public sealed class UtuException : Exception
{
    public UtuException(string message, int exitCode = 1, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    /// <summary>Process exit code that should be used for this failure.</summary>
    public int ExitCode { get; }
}
