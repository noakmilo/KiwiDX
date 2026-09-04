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

Download the latest Windows ZIP from the repository's **Releases** page, extract the entire archive, and run the versioned `KiwiDX` executable. Keep all extracted files together.

Requirements are documented in [`dependences.txt`](dependences.txt).

## Building from source

1. Install the .NET 8 SDK on Windows.
2. Obtain `ffmpeg.exe` from an official KiwiDX release and place it beside `KiwiDX.csproj` if MP4 recording support is required.
3. Run:

```powershell
dotnet restore KiwiDX.csproj
dotnet build KiwiDX.csproj -c Release
```

The application creates its local `favorites.json`, `bookmarks.json`, and `startup.json` data as needed. Example files are included in the repository.

## Author

Built by Kmilo Noa  
Contact: noakmilo90@gmail.com

