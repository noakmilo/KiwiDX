# KiwiDX v0.2.1_beta

WebSDR and Twente use the original hosted page. KiwiDX automatically checks Full window width and sticky once the receiver initializes, and respects subsequent user changes. Experimental page cropping, forced scrolling and canvas resizing have been removed. Existing native tuning and audio controls remain available; the receiver still uses its official web engine.

Validation: local WebView2 tests verify the unmodified layout, checkbox handlers, tuning, bandwidth and audio controls.

## Native OpenWebRX

OpenWebRX now connects directly over WebSocket with native waterfall, analog tuning controls, audio playback and recording. Receiver > OpenWebRX profiles selects advertised SDR profiles. Basic AM, USB, LSB, CW and NFM are supported; digital-mode interfaces, authenticated receivers and HD/stereo streams are outside this beta's coverage. KiwiSDR keeps its own transport and WebSDR retains its hosted page.

Validated directly against openwebrx.nl: audio/spectrum reception, live profile switching, and native WinForms PCM recording without a receiver WebView. Local decoder vectors cover float spectra and fragmented IMA ADPCM synchronization. No compatibility claim is made for every OpenWebRX fork.

Native Airspy SpyServer support: open Map > Airspy SpyServer or enter an sdr://host:5555 address. Includes AM, USB, LSB, NFM and CW, native FFT/waterfall, recording, favorites and RX sharing. Full IQ and WFM are not included. Technical notes: design/protocols/spyserver-native.md.
