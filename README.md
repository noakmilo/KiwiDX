# KiwiDX

KiwiDX is a Windows desktop client for listening to KiwiSDR receivers. It provides live audio, an interactive waterfall, receiver discovery through a map, favorites, frequency bookmarks, startup preferences, and audio/waterfall recording.

## Features

- KiwiSDR audio and waterfall reception
- Frequency, mode, bandwidth, zoom, and spectrum navigation controls
- Receiver map with automatic connection switching
- Server favorites and server-specific frequency bookmarks
- MP3 audio recording at 128 kbps
- 480p MP4 waterfall recording with synchronized audio
- Configurable startup receiver, frequency, playback, and waterfall range

## Running KiwiDX

Run `KiwiDX_v0.1.51_Setup_x64.exe` from **Releases** to install for your Windows user. Install subsequent Setup releases over the existing installation; uninstalling first is unnecessary. The installer blocks downgrades and preserves user data even on uninstall. Alternatively, extract the Windows x64 ZIP and run `KiwiDX.exe`.

The release packages include .NET 8. WebView2 Runtime and Windows Media Foundation are still required. Use **Help > Check for Updates** to compare the installed version with the latest stable GitHub release and open its download page. Updates are checked only when requested; installation is manual.

Requirements are documented in [`dependences.txt`](dependences.txt).

## Building from source

1. Install the .NET 8 SDK on Windows.
2. Obtain `ffmpeg.exe` from an official KiwiDX release and place it beside `KiwiDX.csproj` if MP4 recording support is required.
3. Run:

```powershell
dotnet restore KiwiDX.csproj
dotnet build KiwiDX.csproj -c Release
```

User data lives in `%LOCALAPPDATA%\KiwiDX` (`favorites.json`, `bookmarks.json`, `startup.json`), independently of the application version or installation folder. Changed files have a previous-version `.bak` backup. Keep a separate exported backup for long-term safekeeping.

On launch, missing files are copied from beside the executable without modifying the originals. On a first installation with no startup settings, KiwiDX offers to select an old portable folder. Select the folder containing your old JSON files before starting to use the new version. Existing profile files always take precedence.

Use **Bookmarks > Import/Export Favorites** or **Import/Export Bookmarks** for JSON backups and transfer between computers. Import merges entries, skipping favorites with an existing normalized URL and bookmarks with an existing ID. Existing entries are never replaced. Malformed imports are rejected. Old JSON array files are supported. Bookmark exports include all servers, descriptions, colors and visibility.

## Building release packages

Install [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1 -FfmpegPath C:\path\to\ffmpeg.exe
```

Use `-IsccPath` for a custom compiler location. This publishes a self-contained x64 build with the stable name `KiwiDX.exe` into a fresh staging folder and produces a Setup executable, ZIP and SHA-256 checksums in `dist`. User JSON files are never packaged. Normal source builds retain the versioned executable name.

Publish the generated assets in a GitHub release tagged `v0.1.51` (future releases use their project version). The update checker uses the latest non-prerelease GitHub release. Packaging does not publish anything. Signing the installer requires a publisher certificate; generated installers are currently unsigned.

The installer keeps a fixed AppId and per-user install mode, following [Inno Setup upgrade identity rules](https://jrsoftware.org/ishelp/topic_sameappnotes.htm).

Data preservation checks:

```powershell
dotnet run --project tests/UserData.Tests.csproj -c Release
```

Before public distribution, exercise install, upgrade, downgrade rejection and uninstall in a Windows test account or VM; verify the same saved preferences remain after each operation.

## Author

Built by Kmilo Noa  
Contact: noakmilo90@gmail.com


## Favorites and support

**Bookmarks > Favorites > Manage Favorites** adds and deletes receivers manually. Only KiwiSDR network receivers and the Twente WebSDR exception are supported; an HTTP/HTTPS URL alone does not establish compatibility. New profiles include Twente with `[OFF Kiwi Network]`. Existing favorites, including an intentionally empty list, are preserved.

About includes the GitHub project link and a Donate button. Release ZIP names include `Portable`; settings still live in the Windows user profile.
