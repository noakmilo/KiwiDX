# KiwiDX v0.2.0

- Redesign the native workspace around the spectrum and waterfall, following the approved visual reference.
- Add a resizable right-hand Community Chat dock, an alternate Server details panel, detachable chat and a separate Console Log window.
- Group receiver connection at the top, tuning above the waterfall, zoom/display settings below it, and volume/recording in a fixed bottom toolbar.
- Add a live spectrum trace from receiver data, a blue/cyan/yellow waterfall palette, adaptive MHz ruler, optional band markers and compact Local/RX/UTC clocks.
- Introduce vector-drawn waveform, globe, star, chat, speaker, gear, share, user and info icons, while preserving the original application and installer icon.
- Preserve native chat authentication and private commands, listening logs, receiver protocols, editable favorites and recording behavior.

Validation: workspace layout/resize checks, receiver routing and favorites tests, user data/logging tests, and native chat protocol integration. Live remote receiver and production Turnstile acceptance remain environment-dependent.

- Fix repeated activation of detached chat, console and display windows.
- Move receiver packet processing off the UI thread and bound pending waterfall display work; add a 100,000-frame responsiveness regression.
- Show compact waterfall views for Twente and OpenWebRX, retaining waterfall controls and OpenWebRX profile selection. Local WebView2 integration checks cover visibility, tuning, bandwidth and audio.

- Keep diagnostic history bounded in memory without touching an unopened or hidden Console Log control.
- Add a centered animated connection indicator with stage percentages; test hidden-console startup with oversized diagnostic messages.
