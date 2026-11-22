using System.Security.Cryptography;
using Utu.Core;

namespace Utu.YouTubeApi;

/// <summary>Windows data protection API, scoped to the current user.</summary>
/// <remarks>
/// No key of its own is stored: the keys belong to the Windows user profile, so
/// another user of the same machine cannot read what was protected here.
/// </remarks>
internal static class Dpapi
{
    public static byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw NotWindows("client secrets and tokens");
        }

        return ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public static byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw NotWindows("client secrets and tokens");
        }

        return ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public static UtuException NotWindows(string what) =>
        new(
            $"The {what} are protected with the Windows data protection API, so they can only be " +
            "protected and read on Windows, by the user who protected them.");
}