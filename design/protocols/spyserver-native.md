# Native Airspy SpyServer backend

## Integration and usage

Open **Map**, choose **Airspy SpyServer**, select a marker or search the receiver list, and press **Connect**. A receiver can also be entered directly as `sdr://hostname:5555`; omitted ports default to 5555. Bracketed IPv6 addresses are supported. Credentials, paths, queries, fragments and invalid ports are rejected. No local Airspy hardware or USB driver is required.

`KiwiClient` remains the existing application-facing receiver facade. It delegates SpyServer transport/DSP to `SpyServerClient`, and reuses the existing 12 kHz, signed 16-bit mono NAudio playback and recording pipeline. `Form1` routes `sdr://` before HTTP protocol detection. `HasNativeSpectrum` shares spectrum navigation between OpenWebRX and SpyServer without changing KiwiSDR transport or WebSDR's browser receiver.

The existing frequency, mode, bandwidth, volume, mute, recording, metadata and favorite controls work with this backend. SpyServer mode changes choose AM 6 kHz, USB/LSB 2.4 kHz, NFM 12 kHz and CW 500 Hz defaults; the bandwidth field remains editable. The waterfall uses the existing renderer, marker and history. SpyServer's VHF/UHF coverage does not reduce the available zoom resolution. RX links and favorite JSON preserve `Protocol: SpyServer`; old favorites still default to Auto. Saved `sdr://` URLs also select the backend even when their protocol is Auto.

## Directory discovery

Inspection of the official application's `static/js/main.80d122af.chunk.js` identified **https://airspy.com/directory/status.json**. `AirspyDirectoryService` downloads that structured public endpoint with cancellation, a 15-second timeout and a 4 MB response bound. The server list is not hard-coded.

The model includes owner/name, canonical URL, device, description, antenna, coordinates, coverage, displayed bandwidth, server version, OS, online state and client capacity. Missing fields remain absent/unknown. Entries are cached for the current process so selecting a map receiver supplies metadata to Server details and favorites. The receiver's binary handshake remains authoritative for transport capabilities. A manual URL can connect without downloading the directory.

`ReceiverMapForm` retains the KiwiSDR and Receiverbook map switch and adds Airspy. The Airspy page is generated from structured data using JSON escaping and textContent, not HTML interpolation of remote strings. Only the internally requested generated navigation is allowed; receiver messages must originate from the active map. Offline/full entries show their status, and directory failures offer a retry through the Airspy button. Bundled Leaflet and Natural Earth vectors remove CDN/tile dependencies. Country outlines are intentionally an overview map.

## Wire protocol and independent implementation

The implementation was written independently in C#. No SDR++ or SDRConnect implementation code is linked or copied into the application.

References inspected:

- SDR++ protocol structures and client, revision inspected September 12, 2026: https://github.com/AlexandreRouma/SDRPlusPlus/tree/master/source_modules/spyserver_source/src . Protocol header blob `139e97f281c27afde17cfc84ab994301f1e54b4e`; client blob `c005306284bc5a6a2b2ae4cc4be1b364dd4b0f3a`.
- SDRConnect: https://github.com/isakruas/sdrconnect/blob/master/src/sdrconnect/clients/spyserver.py (Apache-2.0, reference only).
- FFT and decimation cross-check: https://github.com/racerxdl/spy2go/blob/master/spyserver/SpyServer.go (reference only).
- Official ecosystem: https://airspy.com/directory/ .

All integers use little-endian byte order. A command has two uint32 fields (type, body length). HELLO type 0 contains protocol `0x020006a4` (2.0.1700) followed by ASCII client name. SET_SETTING type 2 carries uint32 setting ID and value. A server header has five uint32 fields: protocol version, message type, stream type, sequence, body length. The upper 16 message-type bits carry sample gain. Versions with different major/minor numbers are rejected; compatible build revisions are accepted.

Device information message 0 has twelve uint32 capability fields; client sync message 1 has at least nine uint32 fields with control permission, centers and tuning limits. Extended trailing sync fields are tolerated. Bodies are limited to 1 MiB, capability ranges and sample alignment are validated, and `ReadExactlyAsync` handles fragmented headers/bodies and EOF.

Negotiation requests streaming mode 5 (FFT + IQ), an allowed reduced IQ decimation (sample rate = maximum rate / 2^stage), signed 16-bit IQ unless the server forces another supported format, and uncompressed uint8 FFT. Settings 100/101/102 select IQ format/frequency/decimation; settings 200-205 select FFT format/frequency/decimation/offset/range/pixels. Setting 1 starts/stops streaming.

IQ messages 100/101/102/103 decode unsigned 8-bit, signed little-endian 16/24-bit and float32 samples respectively. Integer values are normalized and compensate the per-packet gain. Float behavior follows the reference implementation; non-finite values are sanitized. Unsupported forced formats are rejected rather than misdecoded.

FFT message 301 contains magnitude bins. Negotiation uses offset 0 dB, range 150 dB, 1200 pixels. A byte maps to `-150 + byte * 150 / 255` dB, then to the existing renderer's dB+255 encoding. Changing the live server's display offset by 20 dB changed levels by approximately 34 bytes, confirming this scale. The display span follows the protocol's 80% usable sample-rate window and selected FFT decimation; bins are projected onto the current native viewport. Differential FFT format 300 is not silently accepted.

## DSP and threading

A 257-tap Blackman-window complex FIR selects the channel, with an oscillator for small local VFO offsets. AM uses an envelope detector; USB/LSB use a complex single-sideband filter and real projection; NFM uses a phase-difference discriminator; CW adds a 700 Hz BFO. Stateful fractional rate conversion produces 24 kHz detector input; a 63-tap audio anti-alias filter decimates to 12 kHz PCM. DC removal and bounded AGC precede output. The SSB audio upper edge is limited to 5.5 kHz by the shared audio output rate.

The client keeps IQ center, selected frequency and FFT center separate. Small tuning changes within the usable IQ window use local frequency translation. Larger changes update the server VFO. Sync messages provide current tuning limits, and older acknowledgments do not immediately undo pending tuning. Shared receivers cannot be tuned outside their advertised accessible range.

TCP receive, coalesced command writing and DSP execute separately from WinForms. IQ uses four bounded pooled packet slots; overload drops incoming packets rather than growing latency indefinitely. The audio facade bounds accumulated playback latency and uses the existing recording writer. The UI already coalesces waterfall frames. Cancellation closes the socket and joins workers; initial negotiation has a 20-second deadline, and stalled receive packets have a 15-second deadline. Console Log diagnostics report TCP and handshake stages, capabilities, transmitted settings, first IQ/FFT and ten-second packet/sample/frame rates and dropped IQ packets. During connection, a five-second progress message identifies the stage and missing streams. TCP, metadata and initial streaming each have a fresh 20-second connection budget; the independent receive watchdog identifies 15-second packet stalls. Directory availability is advisory: it does not prove TCP reachability from the user network.

## Validation

Offline tests: `dotnet run --project tests/SpyServer/SpyServer.Tests.csproj -c Release`. Includes URL validation, framing bounds, IQ conversions, synthetic AM/NFM/CW tone detection, USB/LSB opposite-sideband rejection above 30 dB, a fragmented mock TCP handshake, actual PCM/FFT delivery, local tuning, locked coverage, malformed packets and EOF. No public server is needed.

Optional real test: `dotnet run --project tests/SpyServer/SpyServer.Tests.csproj -c Release -- sdr://HOST:PORT 60`. Downloads the current directory, negotiates the actual server, receives both streams, exercises modes/tuning, continues streaming and reconnects.

Windows integration tests: `tests/Workspace` supports `KIWIDX_TEST_SPY=sdr://HOST:PORT`; it verifies Auto routing, native rendering (no receiver WebView), controls, PCM recording and disconnect. `tests/Maps` verifies all three maps and SpyServer receiver selection. Existing WebReceivers tests cover protocol detection, favorites, RX links and browser receiver behavior.

On September 12, 2026, 42 offline SpyServer checks passed. The live directory returned 217 receivers; a 60-second session delivered 902 FFT frames and 1,442,704 PCM bytes, then reconnected successfully. WinForms SpyServer recording/tuning and native OpenWebRX profile-switch/recording tests passed, as did the three-map and WebSDR browser regressions. The Release build completed with zero warnings and zero errors.

Live development receiver `207.49.195.32:5555` successfully negotiated SpyServer 2.0.1922, Airspy One/R2, 10 MHz maximum rate and 0-35 MHz coverage, using 39062.5 samples/s reduced IQ. This address is a test fixture argument, never a production default.

## Limits and next work

- No Full IQ option or WFM/stereo in this release. Those require a wider audio/DSP path and are deliberately deferred.
- Reduced IQ rates outside 12-500 kHz and differential/compressed formats are rejected with an error. Floating-point and 24-bit conversions have offline coverage; the live test server negotiated 16-bit.
- Reconnect is user initiated. Server capacity, session duration and shared tuning restrictions remain under server control.
- Synthetic tests verify demodulation and sideband rejection; recorded PCM/live spectrum tests do not certify subjective speech quality across every receiver, antenna and signal condition. More hardware/format coverage and longer listening sessions remain useful.
- Future DSP work: stronger multistage resampling at unusually high reduced rates, selectable AGC/de-emphasis/squelch, and a wider PCM path for WFM.
- Only Leaflet 1.9.4 (BSD-2-Clause) and public-domain Natural Earth outlines were added as bundled map assets. No new NuGet/native DSP dependency or GPL implementation was introduced. Leaflet notices accompany installer and portable output.

## Files in this implementation

Created: `SpyServerAddress.cs`, `SpyServerProtocol.cs`, `SpyServerClient.cs`, `SpyServerDemodulator.cs`, `AirspyDirectoryService.cs`, `AirspyMapPage.cs`, `tests/SpyServer/*`, `design/maps/*`, this document.

Integrated/modified: `KiwiClient.cs`, `Form1.cs`, `Form1.CommunityChat.cs`, `ReceiverProtocols.cs`, `ReceiverMapForm.cs`, `ReceiverShare.cs`, `ManageFavoritesForm.cs`, `WaterfallControl.cs`, `KiwiDX.csproj`, `tests/Maps/Program.cs`, `tests/Workspace/Program.cs`, `tests/WebReceivers/Program.cs`, `tests/WebReceivers/WebReceivers.Tests.csproj`, `CHANGELOG.md`, release notes and runtime dependency notes. Other pre-existing working-tree changes were retained.

Timeout diagnostics follow-up: 53 offline checks passed, including a silent-handshake timeout and assertions for each diagnostic event. A separate live Airspy R2 at 135.23.92.149:5556 delivered both streams and reconnected. The WinForms test verified TCP, FFT and disconnect messages in the visible Console Log. In a six-entry directory sample, five endpoints did not answer TCP within the five-second probe; directory online status alone is not a reachability test.

Directory status correction: the official directory uses both `online` and `registered`. `online=true, registered=false` is UNREACHABLE, not READY. KiwiDX now follows this distinction: ready/free is green, offline or unreachable red, busy orange, and missing registered status gray/unverified. The available-only checkbox filters both map markers and list entries. Reloading the Airspy directory refreshes these advisory states; no bulk TCP probing is needed.
