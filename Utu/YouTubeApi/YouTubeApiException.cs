using Google;

namespace Utu.YouTubeApi;

/// <summary>A failure reported by the YouTube Data API.</summary>
public sealed class YouTubeApiException : Exception
{
    public YouTubeApiException(
        string message,
        int? statusCode = null,
        string? reason = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Reason = reason;
    }

    /// <summary>HTTP status code of the failed request, when there was one.</summary>
    public int? StatusCode { get; }

    /// <summary>Machine readable reason reported by the API, when there was one.</summary>
    public string? Reason { get; }

    /// <summary>Whether retrying the same request can succeed.</summary>
    public bool IsTransient => StatusCode is 500 or 502 or 503 or 504;

    /// <summary>Whether the daily quota or upload limit is exhausted.</summary>
    public bool IsQuotaExceeded => Reason is "quotaExceeded" or "uploadLimitExceeded" or "dailyLimitExceeded";

    /// <summary>Whether the request was rejected because of the credentials.</summary>
    public bool IsAuthenticationFailure => StatusCode == 401;

    /// <summary>Wraps a <see cref="GoogleApiException" /> in a <see cref="YouTubeApiException" />.</summary>
    public static YouTubeApiException FromGoogleApiException(GoogleApiException exception, string operation)
    {
        var error = exception.Error?.Errors?.FirstOrDefault();
        var reason = error?.Reason;
        var status = (int)exception.HttpStatusCode;
        var detail = error?.Message;

        var message = reason switch
        {
            "quotaExceeded" or "uploadLimitExceeded" or "dailyLimitExceeded" =>
                "the YouTube API quota or the daily upload limit is exhausted, try again tomorrow",
            "youtubeSignupRequired" =>
                "the authenticated Google account has no YouTube channel",
            "forbidden" or "authError" when status == 401 =>
                "authentication failed, delete the token cache and run utu again",
            _ => detail ?? exception.Message,
        };

        return new YouTubeApiException($"{operation} failed: {message}", status, reason, exception);
    }
}