# utu

`utu` uploads every `.mp4` of a folder to YouTube, using a colourful
[Spectre.Console](https://spectreconsole.net) command line and a single `utu.json`
configuration file.

```
utu <command> [--option=<value>]

    help (default)  show the help
    init            discover the .mp4 files and write utu.json with placeholders
    upload          upload the videos described by utu.json
```

Common options: `--folder=<dir>`, `--config=<file>`, `--only=<pattern>` (repeatable,
`,` separated, `*` wildcards), `--jobs=<n>`, `--retries=<n>`, `--dry-run`,
`--force`, `--no-resume`, `--recursive` (`init`), `--category=<name>` (`init`),
`--verbose`, `--no-color`.

## Getting started

```powershell
# 1. Create an OAuth client: Google Cloud console -> APIs & services ->
#    Credentials -> Create credentials -> OAuth client ID -> Desktop app.
#    Download the JSON and save it as client_secret.json next to utu.json.
#    Enable "YouTube Data API v3" for the project.

utu init --recursive      # writes utu.json, guesses titles, finds thumbnails
# edit utu.json
utu upload --dry-run      # review the plan
utu upload                # upload
```

## `utu.json`

```jsonc
{
  "channel": {
    "name": "My channel",                  // informational, used in reports
    "clientSecretsFile": "client_secret.json",
    "tokenCacheDirectory": ".utu/auth",    // cached OAuth token
    "stateFile": ".utu/state.json",        // upload progress, enables resume
    "user": "default",                     // name of the cached token
    "parallelUploads": 3,
    "resumeUploads": true
  },
  "defaults": {                            // inherited by every entry
    "category": "Music",
    "privacy": "private",
    "madeForKids": false,
    "license": "youtube",
    "embeddable": true,
    "showLikeCount": true
  },
  "videos": [
    {
      "file": "Song.mp4",
      "title": "Song",
      "description": "...",
      "tags": ["tag"],
      "thumbnail": "Song.jpg",
      "playlists": ["Album"],
      "recordingDate": "2024-05-01",
      "location": "52.5163, 13.3777",
      "publishAt": "2024-06-01T12:00:00",
      "enabled": true
    }
  ]
}
```

Every setting is optional per video and falls back to `defaults`. An entry with
`"enabled": false` is skipped. Set `"madeForKids": true` only when the video is
made for kids; it changes which Studio settings apply.

## Upload behaviour

- Videos are uploaded in parallel (`channel.parallelUploads`, `--jobs`).
- Thumbnails, playlists and metadata are applied right after each upload.
- Progress is recorded in `stateFile`; a failed or interrupted upload resumes from
  the stored session URI on the next run (`--no-resume` starts over). `--force`
  uploads a video again even when it already completed.
- Videos that already completed are only revisited when a thumbnail or playlist is
  still missing.

## Settings that need YouTube Studio

`videos.insert` accepts `snippet`, `status`, `recordingDetails.recordingDate` and
`localizations` only. These settings are reported as manual follow-ups after the
upload instead of being set:

collaboration, featured places, allow automatic places, shorts remixing,
comments (on/off), moderation (basic), who can comment, show how many viewers like
this video, and every paid promotion / brand partnership / AI use flag that Studio
does not expose through the API.

## Authentication

`utu` stores no secrets. The first upload opens the browser for the OAuth consent
screen; the resulting token is cached in `tokenCacheDirectory` and refreshed
silently afterwards. Delete that folder to sign out.

## Building and testing

```powershell
dotnet build utu.slnx
dotnet test utu.slnx
```

The upload engine is covered by unit tests behind `IYouTubeClient`, so the test
suite runs without network access or credentials.