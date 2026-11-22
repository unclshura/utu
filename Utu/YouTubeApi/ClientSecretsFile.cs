using System.Security.Cryptography;
using Google.Apis.Auth.OAuth2;
using Utu.Core;

namespace Utu.YouTubeApi;

/// <summary>
/// Reads and protects the OAuth client secrets of the channel.
/// </summary>
/// <remarks>
/// <para>
/// The downloaded secrets are stored unprotected in <c>client_secrets.json</c>.
/// The first time they are used the file is encrypted with the user scoped
/// Windows data protection API (<see cref="Dpapi" />), written as
/// <c>client_secrets.json.protected</c> and the plain file is deleted. Only the
/// protected file is ever read afterwards.
/// </para>
/// <para>
/// No key of its own is stored: the data protection keys belong to the Windows
/// user profile, so another user of the same machine cannot read the file.
/// </para>
/// </remarks>
public static class ClientSecretsFile
{
    /// <summary>Suffix of the protected version of the secrets file.</summary>
    public const string ProtectedSuffix = ".protected";

    /// <summary>Returns the protected path of a secrets file, protected or not.</summary>
    public static string ProtectedPath(string path) =>
        path.EndsWith(ProtectedSuffix, StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ProtectedSuffix;

    /// <summary>Returns the plain path of a secrets file, protected or not.</summary>
    public static string PlainPath(string path) =>
        path.EndsWith(ProtectedSuffix, StringComparison.OrdinalIgnoreCase)
            ? path[..^ProtectedSuffix.Length]
            : path;

    /// <summary>Whether a plain or a protected secrets file exists.</summary>
    public static bool Exists(string path) =>
        File.Exists(PlainPath(path)) || File.Exists(ProtectedPath(path));

    /// <summary>
    /// Protects a plain secrets file, deletes the plain file and returns the
    /// path of the protected file.
    /// </summary>
    /// <remarks>
    /// A protected file that already exists is left alone when there is no
    /// plain file. A plain file always wins, so a freshly downloaded secrets
    /// file replaces an outdated protected one.
    /// </remarks>
    public static string EnsureProtected(string path)
    {
        var plainPath = PlainPath(path);
        var protectedPath = ProtectedPath(path);

        if (!File.Exists(plainPath))
        {
            return protectedPath;
        }

        try
        {
            File.WriteAllBytes(protectedPath, Dpapi.Protect(File.ReadAllBytes(plainPath)));
        }
        catch (Exception exception) when (exception is not (UtuException or OperationCanceledException))
        {
            throw new UtuException(
                $"Cannot protect the OAuth client secrets {plainPath}: {exception.Message}",
                1,
                exception);
        }

        try
        {
            File.Delete(plainPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UtuException(
                $"The OAuth client secrets were protected as {protectedPath}, but {plainPath} " +
                $"could not be deleted: {exception.Message}",
                1,
                exception);
        }

        return protectedPath;
    }

    /// <summary>Reads the client secrets of the protected file, protecting it first if needed.</summary>
    /// <exception cref="UtuException">The file is missing, unreadable or not valid JSON.</exception>
    public static ClientSecrets Load(string path)
    {
        var protectedPath = EnsureProtected(path);

        if (!File.Exists(protectedPath))
        {
            throw new UtuException(
                $"OAuth client secrets not found: {PlainPath(path)}{Environment.NewLine}" +
                "Create an OAuth client of type 'Desktop app' in the Google Cloud console, " +
                $"download its JSON and store it as {PlainPath(path)}; utu protects it on first use.");
        }

        byte[] json;
        try
        {
            json = Unprotect(File.ReadAllBytes(protectedPath), protectedPath);
        }
        catch (UtuException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UtuException(
                $"Cannot read the protected OAuth client secrets {protectedPath}: {exception.Message}",
                1,
                exception);
        }

        try
        {
            using var stream = new MemoryStream(json, writable: false);
            return GoogleClientSecrets.FromStream(stream).Secrets;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UtuException(
                $"The protected OAuth client secrets {protectedPath} are not a valid client secrets file: " +
                $"{exception.Message}{Environment.NewLine}" +
                $"Delete the file and store the downloaded client secrets as {PlainPath(path)} again.",
                1,
                exception);
        }
    }

    private static byte[] Unprotect(byte[] data, string protectedPath)
    {
        try
        {
            return Dpapi.Unprotect(data);
        }
        catch (CryptographicException exception)
        {
            throw new UtuException(
                $"The protected client secrets {protectedPath} cannot be decrypted; they were " +
                $"protected by another Windows user or the file is damaged: {exception.Message}",
                1,
                exception);
        }
    }
}