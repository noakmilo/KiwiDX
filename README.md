# KiwiDX

KiwiDX is a Windows desktop client for listening to KiwiSDR receivers. It provides live audio, an interactive waterfall, receiver discovery through a map, favorites, frequency bookmarks, startup preferences, and audio/waterfall recording.

## Workspace

The receiver toolbar selects a server or favorite; tuning controls sit directly above the live spectrum and waterfall. Zoom, Center and Display settings are below the waterfall. Display settings contains WF Min/Max, optional band markers and tuning steps. Volume, Mute, recording format and elapsed recording time remain in the bottom toolbar; receiver status and Local/RX/UTC clocks are in the status bar.

The reference image is in `design/kiwidx-v0.2.0-reference.png`. Icons are drawn from vector geometry in `WorkspaceTheme.cs`, with PNG previews in `design/icons`.

## Features

- KiwiSDR audio and waterfall reception

- Frequency, mode, bandwidth, zoom, and spectrum navigation controls

- Receiver map with automatic connection switching

- Server favorites and server-specific frequency bookmarks

- MP3 audio recording at 128 kbps

- 480p MP4 waterfall recording with synchronized audio

- Configurable startup receiver, frequency, and waterfall range; automatic audio with Mute beside Volume

## Running KiwiDX

Run `KiwiDX_v0.2.1_Setup_x64.exe` from **Releases** to install for your Windows user. Install subsequent Setup releases over the existing installation; uninstalling first is unnecessary. The installer blocks downgrades and preserves user data even on uninstall. Alternatively, extract the Windows x64 ZIP and run `KiwiDX.exe`.

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

**Bookmarks > Favorites > Manage Favorites** adds and deletes receivers manually. KiwiSDR, OpenWebRX and WebSDR receiver URLs can be added, with automatic or explicit protocol selection. New profiles include Twente with `[OFF Kiwi Network]`. Existing favorites, including an intentionally empty list, are preserved.

About includes the GitHub project link and a Donate button. Release ZIP names include `Portable`; settings still live in the Windows user profile.

Listening logs: right-click the dial and choose **Log Ham Radio...** or **Log Shortwave Listening...**. Automatic fields capture the receiver at entry creation. RX time is marked unavailable when unknown. Enter station details and save; **Logging > View Logs...** shows both categories. Logs persist in %LOCALAPPDATA%\KiwiDX\listening-logs.json, with an automatic backup on subsequent saves. SIMPO has separate S, I, M, P and O fields (1–5).

In either logging category, select a row and click **Export selected report (.txt)...** to save that individual report as a UTF-8 text file.

Select a report in either category to edit its fields or delete it with confirmation. Shortwave uses a SIMPO CODE section with S (Signal), I (Interference), N (Noise), P (Propagation), and O (Overall). Existing third ratings are preserved.

Use **Add manual report...** in either logging category to log a reception on your own radio without connecting to KiwiDX. Rx contains the receiver model (placeholder: XHDATA d-808) for manual reports or the server for waterfall reports. Antenna is entered manually or captured from server information when available. Both fields are included in editing and TXT exports.

Receiver protocols: the editable server dropdown lists favorite URLs and also accepts typed URLs. Choose Auto, KiwiSDR, OpenWebRX, or WebSDR under **Receiver > Protocol**, then Connect (or Enter). Manage Favorites stores a protocol for each manually added receiver; older favorites default to Auto. Auto detection falls back to KiwiSDR if the page cannot be identified, so select the protocol explicitly when needed.

OpenWebRX and WebSDR use an embedded WebView2 receiver page. Keep its band/profile selectors and advanced controls available. KiwiDX synchronizes readable frequency/mode state for logging; unsupported or uninitialized pages cannot create automatic logs. Native recording and native waterfall navigation are disabled for embedded receivers. Antenna and receiver timezone are shown as unavailable when not supplied. Custom WebSDR pages and OpenWebRX variants require receiver-specific acceptance testing.

Validation: `dotnet run --project tests/WebReceivers/WebReceivers.Tests.csproj -c Release -- --basic` checks routing, favorites compatibility and the editable dropdown. Omit `--basic` to run the WebView2 bridge fixtures on Windows. Full bridge tests could not complete in the restricted build environment because WebView2 renderer/browser processes exited; live reception has not been validated for this build.

## Community Chat hosting

Community Chat opens in the right-hand dock and connects automatically. Use **Chat** to hide/show it, drag the divider to resize, or choose **View > Detach chat**. Both channels remain connected when hidden; closing the detached window docks the chat again. **Server details** shares the side area, and **View > Console Log** opens a separate diagnostic window. The waterfall stays visible without page scrolling.

The Python service and deployment files are in [community-chat](community-chat). Follow the [DigitalOcean and invisible Turnstile setup guide](community-chat/DEPLOY_ES.md) for DNS, credentials, HTTPS, systemd, verification and backups. Host the service separately; the client uses https://kiwidx.noakmilo.com; secret keys remain on the server.

Community Chat now uses native WinForms controls and a direct .NET HTTPS/WebSocket client. WebView2 is created only for Turnstile verification and disposed after admission. Deploy `community-chat/static/verify.html` and `verify.js` before using this client. The optional browser chat remains available separately. Run `dotnet run --project tests/CommunityChat/CommunityChat.Tests.csproj -c Release -- PATH_TO_PYTHON_WITH_AIOHTTP` from the repository root to test native protocol integration against a loopback Python fixture.
