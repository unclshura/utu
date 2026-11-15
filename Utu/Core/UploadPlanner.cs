using System.IO.Enumeration;
using Utu.Configuration;

namespace Utu.Core;

/// <summary>Options that influence the upload plan.</summary>
public sealed record UploadPlanOptions
{
    /// <summary>Upload every video again, even if the state says it is done.</summary>
    public bool Force { get; init; }

    /// <summary>Continue interrupted uploads instead of restarting them.</summary>
    public bool Resume { get; init; } = true;

    /// <summary>Only plan the videos that match one of these names or patterns.</summary>
    public IReadOnlyList<string> Only { get; init; } = [];
}

/// <summary>Turns a configuration and the recorded state into an upload plan.</summary>
public sealed class UploadPlanner
{
    private readonly UploadStateStore? _state;

    public UploadPlanner(UploadStateStore? state = null) => _state = state;

    /// <summary>Builds the plan for <paramref name="config" />.</summary>
    /// <param name="config">Loaded configuration.</param>
    /// <param name="workingFolder">Folder that relative paths are resolved against.</param>
    /// <param name="options">Options of the current run.</param>
    public UploadPlan CreatePlan(UtuConfig config, string workingFolder, UploadPlanOptions? options = null)
    {
        var opts = options ?? new UploadPlanOptions();
        var items = new List<UploadPlanItem>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in config.Videos ?? [])
        {
            items.Add(PlanEntry(entry, config.Defaults ?? new VideoSettings(), workingFolder, opts, seen));
        }

        return new UploadPlan { Items = items };
    }

    /// <summary>Matches a file against the <c>--only</c> filters.</summary>
    public static bool MatchesOnly(string file, IReadOnlyList<string> filters)
    {
        if (filters is not { Count: > 0 })
        {
            return true;
        }

        var name = Path.GetFileName(file);

        return filters
            .Where(filter => !string.IsNullOrWhiteSpace(filter))
            .Any(filter =>
                FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: true) ||
                FileSystemName.MatchesSimpleExpression(filter, file, ignoreCase: true));
    }

    private static (UploadAction Action, string Reason) Decide(
        UploadPlanOptions options,
        VideoUploadState? record,
        PendingExtras extras,
        FileIdentity identity)
    {
        if (options.Force)
        {
            return (UploadAction.Upload, "forced by --force");
        }

        if (record is null)
        {
            return (UploadAction.Upload, "new video");
        }

        switch (record.Status)
        {
            case UploadStatus.Completed when record.VideoId is { Length: > 0 } videoId:
                if (!record.Matches(identity))
                {
                    return (UploadAction.Blocked,
                        $"already uploaded as {videoId}, but the file changed since; use --force to upload it again");
                }

                return extras.IsEmpty
                    ? (UploadAction.Skip, $"already uploaded as {videoId}")
                    : (UploadAction.ApplyExtras, $"already uploaded as {videoId}, adding missing thumbnail and playlists");

            case UploadStatus.Completed:
                return (UploadAction.Blocked, "the state file has a completed entry without a video id");

            default:
                if (options.Resume && record.ResumableSessionUri is { Length: > 0 } && record.Matches(identity))
                {
                    return (UploadAction.Resume,
                        $"continuing the interrupted upload ({record.BytesSent:N0} of {record.FileSize:N0} bytes)");
                }

                return record.Status == UploadStatus.Failed
                    ? (UploadAction.Upload, $"retrying after a failure: {record.LastError ?? "unknown error"}")
                    : (UploadAction.Upload, "starting over");
        }
    }

    private static PendingExtras ComputeExtras(
        VideoSettings settings,
        string? thumbnailPath,
        VideoUploadState? record)
    {
        var thumbnail = thumbnailPath is not null &&
                        File.Exists(thumbnailPath) &&
                        record?.ThumbnailApplied != true
            ? thumbnailPath
            : null;

        var playlists = (settings.Playlists ?? [])
            .Where(playlist => !string.IsNullOrWhiteSpace(playlist))
            .Where(playlist => record is null || !record.PlaylistsApplied.Contains(playlist, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new PendingExtras(thumbnail, playlists);
    }

    private UploadPlanItem PlanEntry(
        VideoEntry entry,
        VideoSettings defaults,
        string workingFolder,
        UploadPlanOptions options,
        Dictionary<string, string> seen)
    {
        var settings = SettingsResolver.Resolve(entry, defaults);
        var file = entry.File?.Trim() ?? string.Empty;
        var fullPath = file.Length == 0 ? string.Empty : PathResolver.Resolve(workingFolder, file);
        var thumbnailPath = PathResolver.ResolveOptional(workingFolder, settings.Thumbnail);
        var playlists = settings.Playlists ?? [];

        UploadPlanItem Create(
            UploadAction action,
            string reason,
            PendingExtras? extras = null,
            FileIdentity? identity = null,
            VideoUploadState? record = null,
            IReadOnlyList<ValidationIssue>? issues = null) =>
            new()
            {
                File = file,
                FullPath = fullPath,
                Action = action,
                Reason = reason,
                Settings = settings,
                ThumbnailPath = thumbnailPath,
                Playlists = playlists,
                Extras = extras ?? PendingExtras.None,
                ExistingVideoId = record?.VideoId,
                ResumableSessionUri = action == UploadAction.Resume ? record?.ResumableSessionUri : null,
                BytesSent = action == UploadAction.Resume ? record?.BytesSent ?? 0 : 0,
                FileSize = identity?.Size ?? 0,
                ManualFollowUps = VideoMetadataFactory.DescribeManualFollowUps(settings),
                Issues = issues ?? [],
            };

        if (file.Length == 0)
        {
            return Create(UploadAction.Blocked, "the entry has no 'file'");
        }

        if (!entry.IsEnabled)
        {
            return Create(UploadAction.Skip, "disabled in the configuration");
        }

        if (!MatchesOnly(file, options.Only))
        {
            return Create(UploadAction.Skip, "not selected by --only");
        }

        var key = UploadStateStore.NormalizeKey(file);
        if (!seen.TryAdd(key, file))
        {
            return Create(UploadAction.Blocked, $"duplicate entry, '{seen[key]}' is already planned");
        }

        if (!File.Exists(fullPath))
        {
            return Create(UploadAction.Blocked, "file not found");
        }

        var validation = VideoSettingsValidator.Validate(settings, thumbnailPath);
        if (validation.HasErrors)
        {
            return Create(UploadAction.Blocked, validation.Errors.First().Message, issues: validation.Issues);
        }

        var identity = FileIdentity.FromFile(fullPath);
        var record = _state?.Find(file);
        var extras = ComputeExtras(settings, thumbnailPath, record);
        var (action, reason) = Decide(options, record, extras, identity);

        return Create(action, reason, extras, identity, record, validation.Issues);
    }
}