using System.Text.Json;
using Utu.Configuration;

namespace Utu.Core;

/// <summary>
/// Reads and writes the state file that makes interrupted uploads resumable and
/// keeps already uploaded videos from being uploaded twice.
/// </summary>
public sealed class UploadStateStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly UploadStateDocument _document;

    public UploadStateStore(string path, UploadStateDocument? document = null)
    {
        _path = path;
        _document = document ?? new UploadStateDocument();
    }

    /// <summary>Path of the state file.</summary>
    public string Path => _path;

    /// <summary>
    /// Reads the state file. A missing file yields an empty state; a corrupt file
    /// is renamed so that it is preserved for inspection.
    /// </summary>
    public static UploadStateLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return new UploadStateLoadResult(new UploadStateDocument(), null);
        }

        try
        {
            var document = JsonSerializer.Deserialize<UploadStateDocument>(File.ReadAllText(path), ConfigIO.Options)
                ?? new UploadStateDocument();

            return new UploadStateLoadResult(Normalize(document), null);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            string? warning = null;
            try
            {
                var corrupt = path + ".corrupt";
                File.Move(path, corrupt, overwrite: true);
                warning = $"the state file could not be read ({exception.Message}) and was moved to '{corrupt}'";
            }
            catch (IOException)
            {
                warning = $"the state file could not be read ({exception.Message})";
            }

            return new UploadStateLoadResult(new UploadStateDocument(), warning);
        }
    }

    /// <summary>Path normalized for use as a key.</summary>
    public static string NormalizeKey(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('.', '/');

    /// <summary>Returns the record of a video, or <see langword="null" />.</summary>
    public VideoUploadState? Find(string key)
    {
        lock (_gate)
        {
            return _document.Videos.GetValueOrDefault(NormalizeKey(key));
        }
    }

    /// <summary>Returns the record of a video, creating an empty one when needed.</summary>
    public VideoUploadState GetOrCreate(string key)
    {
        lock (_gate)
        {
            var normalized = NormalizeKey(key);
            if (!_document.Videos.TryGetValue(normalized, out var state))
            {
                state = new VideoUploadState { File = key, Status = UploadStatus.Pending };
                _document.Videos[normalized] = state;
            }

            return state;
        }
    }

    /// <summary>Applies a change to the record of a video.</summary>
    /// <param name="key">Video file, relative to the working folder.</param>
    /// <param name="update">Change to apply.</param>
    /// <param name="persist">Whether to write the state file immediately.</param>
    public void Update(string key, Action<VideoUploadState> update, bool persist = false)
    {
        lock (_gate)
        {
            var state = GetOrCreateUnlocked(key);
            update(state);
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (persist)
        {
            Save();
        }
    }

    /// <summary>Removes the record of a video.</summary>
    public void Remove(string key)
    {
        lock (_gate)
        {
            _document.Videos.Remove(NormalizeKey(key));
        }
    }

    /// <summary>Writes the state file.</summary>
    public void Save()
    {
        lock (_gate)
        {
            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(_document, ConfigIO.Options));
        }
    }

    private VideoUploadState GetOrCreateUnlocked(string key)
    {
        var normalized = NormalizeKey(key);
        if (!_document.Videos.TryGetValue(normalized, out var state))
        {
            state = new VideoUploadState { File = key };
            _document.Videos[normalized] = state;
        }

        return state;
    }

    private static UploadStateDocument Normalize(UploadStateDocument document)
    {
        var videos = new Dictionary<string, VideoUploadState>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in document.Videos)
        {
            videos.TryAdd(NormalizeKey(key), value);
        }

        document.Videos = videos;
        return document;
    }
}