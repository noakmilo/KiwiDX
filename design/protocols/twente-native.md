# Twente native receiver: v0.2.1_beta preparation

Status: investigation only. No native client or beta release has been produced.

## Sources inspected

- http://websdr.ewi.utwente.nl:8901/websdr-sound.js
- http://websdr.ewi.utwente.nl:8901/websdr-waterfall.js
- http://websdr.ewi.utwente.nl:8901/websdr-base.js

The official audio and waterfall source headers explicitly reserve rights and require prior permission for reuse and reverse engineering. Confirm author authorization or obtain an authorized protocol specification before basing a distributed client on these sources. Downloaded inspection files remain under ignored dist/, outside release source archives.

## Existing integration

WebReceiverControl hosts the remote page, calls its JavaScript control functions, and reads tuning state. It is not a native WebSDR protocol implementation. KiwiClient implements the separate KiwiSDR transport; it cannot simply be redirected to Twente.

## Implementation acceptance criteria

1. Independent client receives Twente audio and waterfall with no receiver WebView process.
2. Transport, framing, codec state and error handling are documented from authorized sources.
3. Frequency, mode, passband, volume, mute and waterfall navigation work through native controls.
4. Receiver capabilities prevent unsupported native controls from issuing invalid commands.
5. Each connection owns its cancellation, receive tasks and buffers; switching receivers cannot deliver stale events to the new session.
6. Recorded protocol fixtures cover fragmented messages, malformed frames and decoder resets.
7. Live acceptance covers audio quality, frequency accuracy, waterfall alignment, disconnect/reconnect, server changes and sustained playback.
8. Logging and RX sharing use confirmed receiver state.
9. WebView fallback remains explicitly identified as a web receiver.

## Release naming

User-facing version and artifact filenames: v0.2.1_beta.
Use a valid .NET package prerelease version (0.2.1-beta) and numeric assembly/file version (0.2.1.0). Keep the installer upgrade identity. Generate installer and portable only after the native implementation passes acceptance checks; do not relabel the existing WebView integration as native.
