using System.Security.Cryptography;
using System.Text;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using Utu.Core;

namespace Utu.YouTubeApi;

/// <summary>
/// Caches the OAuth tokens of the YouTube Data API, protected with the Windows
/// data protection API.
/// </summary>
/// <remarks>
/// <para>
/// A token is encrypted before it is written, so the token cache never holds a
/// clear text token and only the Windows user that stored it can read it.
/// </para>
/// <para>
/// A clear text token left behind by an earlier version is read once, written
/// back protected and its clear text file is replaced.
/// </para>
/// </remarks>
public sealed class ProtectedTokenStore : IDataStore
{
    private readonly string _folder;

    /// <param name="folder">Folder that holds one file per cached token.</param>
    public ProtectedTokenStore(string folder) => _folder = folder;

    /// <inheritdoc />
    public Task StoreAsync<T>(string key, T value)
    {
        WriteProtected(PathOf(key), JsonConvert.SerializeObject(value));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync<T>(string key)
    {
        var path = PathOf(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        if (!Directory.Exists(_folder))
        {
            return Task.CompletedTask;
        }

        foreach (var file in Directory.EnumerateFiles(_folder))
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(string key)
    {
        var path = PathOf(key);

        if (!File.Exists(path))
        {
            return Task.FromResult<T>(default!);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UtuException(
                $"Cannot read the cached token {path}: {exception.Message}",
                1,
                exception);
        }

        byte[] plain;
        try
        {
            plain = Dpapi.Unprotect(bytes);
        }
        catch (CryptographicException)
        {
            return Task.FromResult(Migrate<T>(path, bytes));
        }

        return Task.FromResult(Parse<T>(plain, path));
    }

    /// <summary>
    /// Returns the path of the file of a token and rejects names that are not a
    /// plain file name.
    /// </summary>
    private string PathOf(string key) =>
        string.IsNullOrWhiteSpace(key) ||
        key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
        Path.GetFileName(key) != key
            ? throw new UtuException($"'{key}' is not a valid name for a cached token.")
            : Path.Combine(_folder, key);

    /// <summary>Protects a token and replaces the file in one step.</summary>
    private static void WriteProtected(string path, string json)
    {
        var encrypted = Dpapi.Protect(Encoding.UTF8.GetBytes(json));
        var temporary = path + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);

            throw new UtuException(
                $"Cannot protect the cached token {path}: {exception.Message}",
                1,
                exception);
        }
    }

    /// <summary>Protects a clear text token written by an earlier version.</summary>
    private static T Migrate<T>(string path, byte[] bytes)
    {
        var value = Parse<T>(bytes, path);

        if (value is null)
        {
            TryDelete(path);

            return default!;
        }

        WriteProtected(path, JsonConvert.SerializeObject(value));

        return value;
    }

    /// <summary>Parses a cached token, protecting a clear text one on the way.</summary>
    private static T Parse<T>(byte[] bytes, string path)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes))!;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UtuException(
                $"The cached token {path} is damaged: {exception.Message}{Environment.NewLine}" +
                "Delete the file to authorize again.",
                1,
                exception);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: the caller reports the failure that matters.
        }
    }
}