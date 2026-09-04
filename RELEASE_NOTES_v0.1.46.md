# KiwiDX v0.1.46

This release improves KiwiSDR connection compatibility and receiver health detection.

## Changes

- Aligned the WebSocket authentication handshake with the current KiwiSDR protocol.
- Receiver responses are now read immediately during connection setup.
- Removed non-protocol handshake messages rejected by some KiwiSDR proxies.
- The 15-second missing-audio timeout only applies while Play is active.
- Waterfall responsiveness remains monitored independently.
- Waterfall MP4 recordings include synchronized receiver audio.

## Installation

1. Download `KiwiDX_v0.1.46_Windows_x64.zip`.
2. Extract the complete ZIP to a folder.
3. Keep all files together and run `KiwiDX_v0.1.46.exe`.
4. Install the Microsoft .NET 8 Desktop Runtime and Microsoft Edge WebView2 Runtime if Windows reports that either component is missing.

