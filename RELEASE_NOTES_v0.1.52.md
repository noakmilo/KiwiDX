# KiwiDX v0.1.52

- Start maximized; panel switches have a separate row and preserve receiver height with scrolling when screen space runs out.
- Community Chat connects automatically to its configured service with no URL input; green bold heading and restored green Record control.

- Window slogan: Community Driven SDR Listener. Audio starts on connection; Mute replaces Play beside Volume.
- Optional Community Chat with #hamradio/#shortwave tabs, guest nicks, registration/login and Paste RX-Freq tuning links.
- Python chat service with SQLite persistence, invisible Turnstile verification and systemd/Nginx deployment files. Follow community-chat/DEPLOY_ES.md to configure hosting and CAPTCHA keys.
- Ham Radio and Shortwave Listening logging from the dial context menu and Logging menu.
- Automatic server, local time, RX time, UTC time, frequency and band snapshot; Ham Radio also includes mode.
- Ham Radio fields: callsign, QTH, RST and comments.
- Shortwave Listening fields: station callsign, individual S/I/M/P/O ratings, program heard and comments.
- Categorized log history stored in the version-independent user profile with atomic saves and backup.
- RX time is recorded as unavailable when the receiver clock is unknown.
- Export individual Ham Radio or Shortwave Listening reports as UTF-8 .txt files from the log history.
- Added editing and confirmed deletion of individual reports, plus descriptive Shortwave code labels.
- Added manual reports in both categories, Rx receiver labels, antenna capture from KiwiSDR metadata, and receiver/antenna placeholders.
- Embedded OpenWebRX and WebSDR receivers, editable server/favorites dropdown and saved protocol selection. Web receivers use their own band/profile panels; native recording remains unavailable.
- Fixed false OpenWebRX detection on KiwiSDR pages; detector uses interface/script markers and ignores descriptions and HTML comments. Switching URLs restores Auto or the saved favorite protocol.
