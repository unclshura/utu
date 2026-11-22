using Utu.Configuration;

namespace Utu.Tests;

/// <summary>A temporary folder that is deleted when the test finishes.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "utu-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Creates a file with the given content and returns its absolute path.</summary>
    public string WriteFile(string relativePath, string content = "video")
    {
        var full = this[relativePath];
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Creates a directory and returns its absolute path.</summary>
    public string CreateDirectory(string relativePath)
    {
        var full = this[relativePath];
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Returns the absolute path of a file inside the folder.</summary>
    public string this[string relativePath] => System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Shortcuts for the configuration objects used by the tests.</summary>
internal static class TestConfig
{
    public static VideoEntry Entry(string file, string? title = null) =>
        new()
        {
            File = file,
            Title = title ?? Path.GetFileNameWithoutExtension(file),
        };

    public static UtuConfig With(params VideoEntry[] entries) =>
        new()
        {
            Channel = new ChannelConfig { Name = "Test channel" },
            Videos = [.. entries],
        };

    public static UtuConfig WithDefaults(DefaultsSettings defaults, params VideoEntry[] entries) =>
        new()
        {
            Channel = new ChannelConfig { Name = "Test channel" },
            Defaults = defaults,
            Videos = [.. entries],
        };
}
