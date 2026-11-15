namespace Utu.Core;

/// <summary>How a planned item ended.</summary>
public enum UploadOutcomeStatus
{
    /// <summary>The video was uploaded as a new video.</summary>
    Uploaded,

    /// <summary>An interrupted upload was continued.</summary>
    Resumed,

    /// <summary>Only thumbnail or playlists were added to an existing video.</summary>
    Updated,

    /// <summary>The item could not be processed.</summary>
    Failed,
}

/// <summary>Result of one planned item.</summary>
/// <param name="Item">The planned item.</param>
/// <param name="Status">What happened.</param>
/// <param name="VideoId">Id of the video, when it is known.</param>
/// <param name="Error">Failure message, when the item failed.</param>
/// <param name="Notes">Warnings and follow up work.</param>
public sealed record UploadOutcome(
    UploadPlanItem Item,
    UploadOutcomeStatus Status,
    string? VideoId,
    string? Error,
    IReadOnlyList<string> Notes)
{
    /// <summary>Whether the item was processed successfully.</summary>
    public bool Succeeded => Status != UploadOutcomeStatus.Failed;

    /// <summary>Link to the video, when it has an id.</summary>
    public string? VideoUrl => VideoId is { Length: > 0 } ? $"https://youtu.be/{VideoId}" : null;
}

/// <summary>Result of a whole run.</summary>
public sealed record UploadSummary(IReadOnlyList<UploadOutcome> Outcomes)
{
    /// <summary>Items that were processed successfully.</summary>
    public IEnumerable<UploadOutcome> Succeeded => Outcomes.Where(outcome => outcome.Succeeded);

    /// <summary>Items that failed.</summary>
    public IEnumerable<UploadOutcome> Failed => Outcomes.Where(outcome => !outcome.Succeeded);

    /// <summary>Items with warnings or manual follow up work.</summary>
    public IEnumerable<UploadOutcome> WithNotes => Outcomes.Where(outcome => outcome.Notes.Count > 0);

    /// <summary>Whether anything failed.</summary>
    public bool HasFailures => Outcomes.Any(outcome => !outcome.Succeeded);
}