# utu

`utu` uploads every `.mp4` of a folder to YouTube, using a colourful
[Spectre.Console](https://spectreconsole.net) command line and layered `utu.json`
configuration files.

```
utu <command> [--option=<value>]

    help (default)  show the help
    init            discover the .mp4 files and write utu.json with placeholders
    schedule        recompute the publishAt of every video from publishSchedule
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
#    Download the JSON and save it as %USERPROFILE%\.utu\client_secrets.json
#    (created by hand, next to the global utu.json). The first upload replaces it
#    with client_secrets.json.protected, see Authentication.
#    Enable "YouTube Data API v3" for the project.

# 2. Describe the channel once in %USERPROFILE%\.utu\utu.json (see below).
#    The first utu init writes this file with every setting and the value utu
#    would use anyway; fill in the channel name there.
utu init --recursive      # writes utu.json, guesses titles, finds thumbnails
# edit utu.json
utu upload --dry-run      # review the plan
utu upload                # upload
```

## Layered configuration

Settings come from three layers. The first layer that defines a value wins:

| # | Layer | File |
|---|-------|------|
| 1 | the video entry | `<folder>/utu.json` |
| 2 | the folder defaults | `<folder>/utu.json` (`defaults`) |
| 3 | the global configuration | `%USERPROFILE%\.utu\utu.json` (`defaults` and `channel`) |
| 4 | the built in values of `utu` | - |

So one home folder holds the channel parameters, the secrets and everything that
every video shares:

```jsonc
// %USERPROFILE%\.utu\utu.json
{
  "channel": {
    "name": "My channel",
      "clientSecretsFile": "client_secrets.json",   // -> ~/.utu/client_secrets.json
    "tokenCacheDirectory": "auth",                // -> ~/.utu/auth
    "parallelUploads": 3,
    "resumeUploads": true
  },
  "defaults": {
    "category": "Music",
    "license": "youtube",
    "embeddable": true,
    "showLikeCount": true
  }
}
```

and a folder only repeats what differs:

```jsonc
// <folder>/utu.json
{
  "defaults": { "tags": ["album"] },
  "videos": [
    { "file": "Song.mp4", "title": "Song", "thumbnail": "Song.jpg" }
  ]
}
```

`utu init` writes that `defaults` block with every available setting and leaves it
`null`, so the file shows what can be set without overriding the inherited value:

```jsonc
// <folder>/utu.json, written by utu init
{
  "defaults": {
    "title": null,                  // null = inherit; a value applies to every video
    "description": null,
    "tags": null,
    "thumbnail": null,
    "category": null,               // filled when init was told --category=<name>
    "privacy": null,
    "recordingDate": null,
    "publishAt": null,
    "publishSchedule": null,        // defaults only, see Publication schedule
    // ... every other setting of the section below
  },
  "videos": [
    { "file": "Song.mp4", "title": "Song", "thumbnail": "Song.jpg" }
  ]
}
```

A few rules keep the layers predictable:

- merging is per setting; lists (`tags`, `playlists`, `featuredPlaces`,
  `collaboration`) and `localizations` are replaced as a whole, not concatenated,
- a blank or missing value means "not defined here", so it is inherited,
- relative paths are resolved against the folder of the file that defines them,
  so a path named by the global `utu.json` belongs in `~/.utu`,
- settings that no layer defines fall back to built-ins, and those paths live in
  `~/.utu`: `client_secrets.json` and the token cache `auth` (the state file
  stays next to the videos, in `<folder>/.utu/state.json`),
- `--config=<file>` names an exact file that is used as is, without the global
  layer,
- the first `utu init` writes the global `utu.json` when it is missing: every
  setting with the value `utu` would use anyway, so the file doubles as
  documentation. A file that exists is never overwritten, and blank values keep
  meaning "use the built in default",
- `utu init` writes the `utu.json` of a folder with a `defaults` block that names
  every available setting as a `null` placeholder; video entries only carry
  `file`, `title` and `thumbnail`, so nothing is overridden by accident,
- `publishSchedule` belongs in `defaults` only. A video entry that names it is
  rejected, because the schedule decides the order of the publications itself
  (see [Publication schedule](#publication-schedule)),
- `UTU_HOME` moves the global folder (handy for tests, see below).

## `utu.json`

```jsonc
{
  "channel": {
    "name": "My channel",                  // informational, used in reports
    "clientSecretsFile": "client_secrets.json",
    "tokenCacheDirectory": "auth",         // -> ~/.utu/auth, cached OAuth token
    "stateFile": ".utu/state.json",        // -> <folder>/.utu/state.json, enables resume
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
    "showLikeCount": true,
    "publishSchedule": null                // crontab, spreads publishAt over the videos
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

Every setting is optional per video and falls back to `defaults` of the folder and
then to the global configuration. An entry with `"enabled": false` is skipped. Set
`"madeForKids": true` only when the video is made for kids; it changes which
Studio settings apply.

## Publication schedule

`publishSchedule` is a crontab expression that only belongs in `defaults`. `utu
init` uses it to give every new video an exact `publishAt`: the first time the
expression matches after the run goes to the first video in file order, the next
time to the second, and so on. Videos that already carry a `publishAt` keep it.
Without a `publishSchedule` no `publishAt` is written at all.

`utu schedule` uses the same expression to recompute the times of the videos of
an existing `utu.json`, which is what to run after the video list or the schedule
changed: every video gets the next time in file order and the old `publishAt` is
replaced. `--dry-run` reports the new times without writing the file.

```jsonc
// %USERPROFILE%\.utu\utu.json
{
  "defaults": {
    "privacy": "private",             // required, see below
    "publishSchedule": "0 9 * * *"    // one video a day at 09:00
  }
}
```

The fields are minute, hour, day of month, month and day of week; an optional
leading field adds seconds:

```
┌───────── minute (0-59)
│ ┌─────── hour (0-23)
│ │ ┌───── day of month (1-31)
│ │ │ ┌─── month (1-12, JAN-DEC)
│ │ │ │ ┌─ day of week (0-6, SUN-SAT, 7 = Sunday)
│ │ │ │ │
0 9 * * *     every day at 09:00
0 */6 * * *   every six hours
30 8 * * 1    Mondays at 08:30
* 9-17 * * MON-FRI   every minute during office hours
@daily        once a day at midnight
@hourly       once an hour, on the hour
```

A field accepts `*`, a list (`1,15`), a range (`8-17`), a step (`*/15`, `8-17/2`),
names for months and weekdays (`JAN`, `MON-FRI`) and the macros `@hourly`,
`@daily`, `@midnight`, `@weekly`, `@monthly`, `@yearly` and `@annually`. Times are
read in the local time zone.

Two things are worth knowing:

- YouTube accepts a publication time only on a private video, so `privacy` has to
  resolve to `private`; `utu` warns when it does not.
- An expression that cannot match within the next ten years, or one that names
  fewer times than there are videos to schedule, stops `init` with exit code 2
  instead of writing half a schedule.

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

The first upload opens the browser for the OAuth consent screen; the resulting
token is cached in `~/.utu/auth` (`tokenCacheDirectory`) and refreshed silently
afterwards. The token is encrypted with the same user scoped Windows data
protection API before it is written, so the cache never holds a clear text token
and only the Windows user that authorized can read it. A clear text token left by
an earlier version is protected the first time it is read. Delete the folder to
sign out.

The downloaded OAuth client secrets are plain JSON, so `utu` protects them with
the user scoped Windows data protection API
(`System.Security.Cryptography.ProtectedData`) the first time they are used:

```text
~/.utu/client_secrets.json            <- you store the downloaded file here
~/.utu/client_secrets.json.protected  <- utu writes this and deletes the plain file
```

Only the protected file is read afterwards. Drop a new `client_secrets.json` next
to it to replace the secrets; the next run protects the new file and deletes the
plain one again. The key belongs to the Windows user profile, so another user of
the same machine cannot decrypt the file, and copying it to another machine or
user makes it unreadable: restore the plain JSON there to protect it again.

## Building and testing

```powershell
dotnet build utu.slnx
dotnet test utu.slnx
```

The upload engine is covered by unit tests behind `IYouTubeClient`, so the test
suite runs without network access or credentials.
