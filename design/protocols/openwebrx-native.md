# Native OpenWebRX integration

KiwiDX connects to the hosted /ws/ endpoint and identifies as a receiver. JSON commands negotiate a 12 kHz output rate, start DSP, tune relative to the active profile center and select profiles. Binary type 1 contains float32 little-endian spectrum or IMA ADPCM spectrum with ten leading padding samples; type 2 contains PCM16 little-endian or synchronized IMA ADPCM audio. Audio synchronization headers and codec state survive WebSocket message boundaries. FFT codec state is reset per spectrum frame.

The implementation was written for KiwiDX against the publicly available OpenWebRX protocol behavior, without bundling or executing the hosted JavaScript. Standard IMA ADPCM tables and decoding arithmetic are used.

Reference implementations:
- https://github.com/jketterl/openwebrx/blob/develop/owrx/connection.py
- https://github.com/jketterl/openwebrx/blob/develop/htdocs/openwebrx.js
- https://github.com/jketterl/openwebrx/blob/develop/htdocs/lib/AudioEngine.js

Receiver > OpenWebRX profiles exposes advertised profile names. Frequencies outside an active profile are rejected and the tuning display returns to the current frequency. Native basic analog modes are supported; digital mode interfaces, HD/stereo streams and authenticated receivers are not covered by this beta.

Validation: decoder vectors, fragmented synchronization header, live direct audio/spectrum and profile switching at https://openwebrx.nl/, and an end-to-end WinForms connection/PCM recording test with no receiver WebView. The example nc-home.tgws.de:8773 endpoint timed out. Compatibility with every OpenWebRX derivative is not claimed.
