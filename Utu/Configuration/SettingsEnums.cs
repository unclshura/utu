namespace Utu.Configuration;

/// <summary>Visibility of an uploaded video (<c>status.privacyStatus</c>).</summary>
public enum Privacy
{
    Private,
    Unlisted,
    Public,
}

/// <summary>Licence of an uploaded video (<c>status.license</c>).</summary>
public enum VideoLicense
{
    Youtube,
    CreativeCommon,
}

/// <summary>
/// Remix permission for Shorts. YouTube only exposes this in YouTube Studio,
/// utu records the value and reports it as a manual follow up.
/// </summary>
public enum ShortsRemixing
{
    VideoAndAudio,
    AudioOnly,
    None,
}

/// <summary>Comment availability. YouTube Studio only.</summary>
public enum CommentPolicy
{
    On,
    Off,
}

/// <summary>Comment moderation. YouTube Studio only.</summary>
public enum CommentModeration
{
    Basic,
    Strict,
    HoldAll,
    HoldPotentiallyInappropriate,
}

/// <summary>Who is allowed to comment. YouTube Studio only.</summary>
public enum CommentAudience
{
    Everyone,
    Subscribers,
    None,
}
