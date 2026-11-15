namespace Utu.Core;

/// <summary>Severity of a validation message.</summary>
public enum ValidationSeverity
{
    Warning,
    Error,
}

/// <summary>One problem found in a video configuration.</summary>
/// <param name="Severity">Whether the problem blocks the upload.</param>
/// <param name="Setting">Name of the setting that caused the problem.</param>
/// <param name="Message">Human readable description.</param>
public sealed record ValidationIssue(ValidationSeverity Severity, string Setting, string Message)
{
    public override string ToString() => $"{Setting}: {Message}";
}

/// <summary>The outcome of validating a video configuration.</summary>
public sealed class ValidationResult
{
    private readonly List<ValidationIssue> _issues = [];

    public IReadOnlyList<ValidationIssue> Issues => _issues;

    public bool HasErrors => _issues.Any(issue => issue.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Errors =>
        _issues.Where(issue => issue.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Warnings =>
        _issues.Where(issue => issue.Severity == ValidationSeverity.Warning);

    public void Error(string setting, string message) =>
        _issues.Add(new ValidationIssue(ValidationSeverity.Error, setting, message));

    public void Warn(string setting, string message) =>
        _issues.Add(new ValidationIssue(ValidationSeverity.Warning, setting, message));

    /// <summary>Adds the issues of another result to this one.</summary>
    public void AddRange(ValidationResult other) => _issues.AddRange(other._issues);
}
