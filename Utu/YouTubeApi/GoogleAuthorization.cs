using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Utu.Configuration;
using Utu.Core;

namespace Utu.YouTubeApi;

/// <summary>
/// Interactive OAuth authorization for the YouTube Data API.
/// </summary>
/// <remarks>
/// The OAuth client secrets are supplied by the user; no secrets are stored in
/// the repository. The resulting token is cached in the folder configured by
/// <see cref="ChannelSettings.TokenCacheDirectory" />.
/// </remarks>
public static class GoogleAuthorization
{
    /// <summary>Scopes needed to upload videos and manage their metadata.</summary>
    public static readonly string[] Scopes =
    [
        YouTubeService.Scope.Youtube,
        YouTubeService.Scope.YoutubeUpload,
    ];

    /// <summary>Creates an authorized <see cref="YouTubeService" />.</summary>
    /// <param name="channel">Channel settings, including absolute token cache folder.</param>
    /// <param name="beforeInteractiveLogin">
    /// Called once when no cached token exists and a browser window is about to
    /// open.
    /// </param>
    /// <param name="cancellationToken">Cancels the authorization.</param>
    public static async Task<YouTubeService> CreateServiceAsync(
        ChannelSettings channel,
        Action<string>? beforeInteractiveLogin = null,
        CancellationToken cancellationToken = default)
    {
        var credential = await AuthorizeAsync(channel, beforeInteractiveLogin, cancellationToken)
            .ConfigureAwait(false);

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "utu",
        });
    }

    /// <summary>Authorizes the user, reusing a cached token when one exists.</summary>
    public static async Task<UserCredential> AuthorizeAsync(
        ChannelSettings channel,
        Action<string>? beforeInteractiveLogin = null,
        CancellationToken cancellationToken = default)
    {
        var secretsPath = channel.ClientSecretsFile;
        if (!File.Exists(secretsPath))
        {
            throw new UtuException(
                $"OAuth client secrets not found: {secretsPath}{Environment.NewLine}" +
                "Create an OAuth client of type 'Desktop app' in the Google Cloud console, " +
                "download its JSON and store it there.");
        }

        var tokenDirectory = channel.TokenCacheDirectory;
        Directory.CreateDirectory(tokenDirectory);

        if (beforeInteractiveLogin is not null && !HasCachedToken(tokenDirectory))
        {
            beforeInteractiveLogin(tokenDirectory);
        }

        ClientSecrets secrets;
        try
        {
            using var stream = File.OpenRead(secretsPath);
            secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UtuException($"Cannot read the OAuth client secrets {secretsPath}: {exception.Message}", 1, exception);
        }

        try
        {
            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    Scopes,
                    channel.User,
                    cancellationToken,
                    new FileDataStore(tokenDirectory, fullPath: true),
                    codeReceiver: null)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new UtuException(
                $"Authentication failed: {exception.Message}{Environment.NewLine}" +
                $"Delete '{tokenDirectory}' to discard the cached token and try again.",
                1,
                exception);
        }
    }

    private static bool HasCachedToken(string tokenDirectory) =>
        Directory.Exists(tokenDirectory) &&
        Directory.EnumerateFiles(tokenDirectory, "*", SearchOption.AllDirectories).Any();
}