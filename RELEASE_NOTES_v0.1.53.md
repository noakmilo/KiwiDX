# KiwiDX v0.1.53

- Start maximized with three control rows: receiver settings, volume/recording, and compact panel switches with solid backgrounds.
- Preserve receiver height when showing optional panels; grow restored windows where possible and scroll when screen space runs out.
- Use the Community Driven SDR Listener slogan, automatic receiver audio, Mute beside Volume, and a green Record control.
- Add native Community Chat with nick controls, simultaneous #hamradio/#shortwave tabs, private /help and registration/login commands, and clickable RX/frequency/mode links.
- Connect directly to the configured Python service using HTTPS and WebSocket. WebView2 is used temporarily for Turnstile verification only, never to render the chat.
- Keep registered nicks and recent channel history in SQLite; include systemd/Nginx configuration, CAPTCHA verification files and a DigitalOcean deployment guide.

Deploy static/verify.html and static/verify.js from the chat server package before using the native chat client.
