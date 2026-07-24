# Jellyfin.Plugin.HistraNet

Scrobbles Jellyfin playback to [histra.net](https://histra.net) — media tracking
like Trakt.

## Install

In Jellyfin: **Dashboard → Plugins → Repositories → +** and add this URL:

```
https://raw.githubusercontent.com/Histra-net/JellyfinPlugin/master/manifest.json
```

Then **Catalog → histra.net → Install** and restart the server. Requires
Jellyfin **10.11**.

## What it does

Listens to Jellyfin playback events (`PlaybackStart` / `PlaybackProgress` /
`PlaybackStopped`) and forwards them to the histra.net tracking API:

| Jellyfin event        | histra action              |
|-----------------------|----------------------------|
| Playback start        | `start`                    |
| Progress (throttled)  | `start` (or `pause` when paused) |
| Playback stopped      | `stop`                     |

Titles are matched by their **provider ids** (TMDb / IMDb / TVDB) read from the
Jellyfin item — histra.net looks them up itself, so no ids need to be stored
locally. Movies scrobble by `movie`, episodes by `show` + `episode` (season +
number). Sufficiently completed playback is automatically marked as watched by histra.net
on `stop`.

Scrobbling is **per Jellyfin user**: each user's playback is sent under their
own personal histra.net token. Users without a configured token are not
scrobbled (playback is never attributed to another user's histra account).

## Configuration

Dashboard → Plugins → **histra.net**:

- **Server URL** — default `https://histra.net`.
- **Scrobble movies / episodes** — toggle per media type.
- **Progress report interval (%)** — minimum percent change between progress
  updates sent to the API (throttling).
- **Per-user tokens** — one row per Jellyfin user; paste that user's personal
  token (`hst_...`) and use **Test** to verify it against `/api/v1/auth/me`.
- **Import (histra.net → Jellyfin)** — skip unwatched / watched / playback
  progress import, applied by the scheduled task.
- **Export (Jellyfin → histra.net)** — set watched/unwatched during the
  scheduled task, and set watched/unwatched immediately on change during
  normal use.
- **Enable debug logging** — verbose logging of sync/scrobble activity.

## Synchronization

Two directions, matching Trakt:

- **Scrobble** (real time) — playback start/progress/stop → histra.net.
- **Realtime export** (real time) — toggling watched/unwatched on an item is
  pushed to histra.net immediately (`WatchStateExporter`, listens to
  `UserDataSaved`). Imports made by the sync task are ignored to avoid a loop.
- **Scheduled task "Sync with histra.net"** (default every 24 h, runnable
  on demand from Dashboard → Scheduled Tasks):
  - **Import** — pages through histra.net history, matches each entry to the
    library by `external_ids` (movie by its ids; episode by the series' ids +
    season/episode number) and marks it watched locally.
  - **Playback progress import** — reads the continue-watching endpoints and
    sets the resume position (`PlaybackPositionTicks`) on matching library
    items, so in-progress titles appear in Jellyfin's "Continue Watching".
    Fully-watched items are never overwritten with a resume position.
  - **Export** — pushes locally-played movies and episodes to histra.net as
    watched. Bulk unwatched export is intentionally not performed (that would
    risk wiping histra state set by other clients); unwatched is handled by the
    realtime exporter on toggle.

### Token source

Selectable in the config (**Token source**), resolved per call by
`RoutingUserTokenProvider`:

- **config** — `ConfigUserTokenProvider` reads the per-user token map above.
- **manager** — `ManagerUserTokenProvider` looks the token up from the company
  manager over HTTP. The plugin POSTs JSON `{ "apptoken": "<service token>",
  "userid": "<jellyfin user id>" }` to the configured **Manager URL** and
  expects `{ "token": "hst_..." }` back (empty/absent → that user is not
  scrobbled). Results are cached ~10 minutes. Switching the source in the UI
  takes effect without a restart.

## Building

```
dotnet build -c Release
```

Output: `Jellyfin.Plugin.HistraNet/bin/Release/net9.0/Jellyfin.Plugin.HistraNet.dll`.

Copy the DLL into a `histra.net_<version>` folder under your Jellyfin `plugins`
directory (with a `meta.json`) and restart the server. Targets Jellyfin
**10.11** (ABI 10.11.0.0, net9.0).

## Roadmap

- Confirm the exact manager request/response field names against the real
  manager endpoint (currently `apptoken` / `userid` / `token`).

## Releasing

Run `release.bat 1.0.0.1` (or just `release.bat` and enter the version). It
tags `v<version>` and pushes it; GitHub Actions then builds the DLL, packages
the ZIP, publishes a GitHub Release, and updates `manifest.json` so Jellyfin
clients see the new version. No manual checksum or upload needed.

## License

[MIT](LICENSE) © histra.net
