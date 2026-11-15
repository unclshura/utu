namespace Utu.Core;

/// <summary>
/// Size and timestamp of a file, used to detect that a video changed since it
/// was uploaded.
/// </summary>
public readonly record struct FileIdentity(long Size, long WriteTimeUtcTicks)
{
    /// <summary>Reads the identity of an existing file.</summary>
    public static FileIdentity FromFile(string path)
    {
        var info = new FileInfo(path);
        return new FileIdentity(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    /// <summary>Returns <see langword="true" /> when the file still has this identity.</summary>
    public bool Matches(string path)
    {
        try
        {
            return File.Exists(path) && FromFile(path) == this;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Returns a short description of the identity, for reports.</summary>
    public string Describe() =>
        $"{Size:N0} bytes, {new DateTime(WriteTimeUtcTicks, DateTimeKind.Utc):u}";
}
