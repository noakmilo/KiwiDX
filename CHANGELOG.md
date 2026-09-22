## v0.2.1 - 2026-09-13

- Edit favorite Name and Description while keeping URL and Antenna read-only; preserve custom descriptions when receiver metadata refreshes.

- Honor Airspy directory registered/reachability status: green ready receivers, red offline/unreachable, orange busy and gray unverified; add a legend and available-only map/list filter.

- Route SpyServer diagnostics to Console Log, including TCP/handshake stages, capabilities, settings, first IQ/FFT, periodic rates and precise timeout causes. Give TCP, negotiation and streaming separate connection budgets.

- Add native Airspy SpyServer TCP reception with reduced IQ + FFT, native spectrum/waterfall, AM/USB/LSB/NFM/CW demodulation, tuning, audio and recording.
- Add the live Airspy directory as a third receiver map, with bundled vector cartography, searchable receiver metadata and direct sdr:// connections.
- Preserve SpyServer favorites and RX links; validate network packets, bound processing buffers and handle disconnect/reconnect cleanly.

- For Twente only, scroll to the bottom edge of the waterfall and lock vertical scrolling with the scrollbar hidden.

- Populate the native Band selector with the active OpenWebRX server profiles; selecting a band changes the server profile and clears the previous waterfall history. Restore standard bands for KiwiSDR.

- Receive OpenWebRX audio and spectrum directly without WebView; add native profile selection, tuning, waterfall navigation and recording.
- Decode PCM/float spectra and IMA ADPCM streams; preserve the selected receiver protocol in RX sharing.

- Keep the original WebSDR/Twente page layout; automatically enable Full window width and sticky when connecting.
- Remove experimental WebSDR page cropping, forced scrolling and canvas resizing. OpenWebRX retains its existing compact view.

## v0.2.0 - 2026-09-11

- Preserve the explicitly selected map/favorite receiver URL throughout asynchronous disconnect, detection and connection.

- Switch between KiwiSDR and Receiverbook maps with a floating button; connect receiver marker links directly and hide Receiverbook navigation/footer.

- Enable Full window width and sticky automatically when a WebSDR receiver initializes, including Twente.

- Label bands as ham radio or broadcast; show favorite URLs with location and antenna in the editable receiver dropdown.

- Keep bounded console history in memory and update the native log control only when visible; truncate oversized diagnostics.
- Show a centered animated connection indicator with stage percentages.

- Add the waterfall-focused workspace with a detachable native chat sidebar.
- Safely reactivate detached chat, console and display windows, including minimized windows.
- Receive audio and waterfall off the UI thread, cap display refreshes and sample packet diagnostics to prevent UI queue overload.
- Show only waterfall and its controls in Twente/OpenWebRX; retain OpenWebRX profile selection and receiver error/start overlays.
- Bound receiver socket shutdown and restore the original application and installer icon.

## v0.1.53 - 2026-09-10

- Start maximized with three control rows: receiver settings, volume/recording, and compact panel switches with solid backgrounds.
- Preserve receiver height when showing optional panels; grow restored windows where possible and scroll when screen space runs out.
- Use the Community Driven SDR Listener slogan, automatic receiver audio, Mute beside Volume, and a green Record control.
- Add native Community Chat with nick controls, simultaneous #hamradio/#shortwave tabs, private /help and registration/login commands, and clickable RX/frequency/mode links.

## v0.1.52 - 2026-09-09

- Fixed automatic protocol detection: KiwiSDR uses the native client even when its description mentions OpenWebRX; changing URLs resets the previous protocol selection.
- Added embedded OpenWebRX and general WebSDR receivers, automatic/manual protocol selection, and protocol-aware favorites.
- Server URL is now an editable favorites dropdown; receiver state is synchronized for logging.
- Web receivers retain their band/profile controls; native recording is unavailable for embedded receivers.
- Added Ham Radio and Shortwave Listening logs from the dial context menu and Logging menu.
- Capture receiver, local/RX/UTC times, frequency and band; Ham Radio also captures mode.
- Separate station details, SIMPO text fields, program and comments with persistent categorized log history.

## v0.1.51 - 2026-09-07

- About includes GitHub and PayPal Donate links with an "Enjoying KiwiDX?" support message.
- New profiles default to Twente WebSDR, labeled [OFF Kiwi Network]; existing favorites are preserved.
- Favorites submenu always provides Manage Favorites to manually add and delete receivers, with supported receiver guidance and URL/duplicate validation.
- Added JSON import/export for Favorites and Bookmarks, with validation and merge semantics (existing bookmark IDs and normalized favorite URLs win).
- Store user data in %LOCALAPPDATA%\KiwiDX across versions; migrate missing files from beside the executable or an old portable folder selected on first launch.
- Save changed data atomically and keep the previous file as .bak.
- Added manual GitHub release version checks under Help; downloads and installation remain user initiated.
- Added a repeatable self-contained Windows x64 ZIP and Inno Setup installer build with fixed upgrade identity, stable installed executable name, downgrade protection, and preserved user data on uninstall.
- Move receiver-map WebView2 data into the user profile.

# Version history

## 0.1.49

- Fixed RX clock initialization by recognizing channel announcements on both streams and preserving spaces in receiver time metadata.
- Added bottom-right Local time (green), RX time (yellow), and UTC (red) clocks with bold seven-segment digits; RX uses receiver-reported local time, with Netherlands daylight-saving support for Twente.
- Replaced the separate Twente window with a hybrid bridge embedded in the main KiwiDX waterfall area.
- KiwiDX frequency, mode, bandwidth, audio, volume, and basic waterfall controls now drive the official Twente WebSDR client.
- Switching to any other server address fully restores the native KiwiSDR connection and display.
- The University of Twente WebSDR can be added to Favorites manually like any other receiver.
- The receiver map can be opened while the embedded Twente WebSDR remains connected.
- Restored the native frequency scale immediately when switching from Twente back to a KiwiSDR.

## 0.1.48

- Added hidden compatibility for the University of Twente WebSDR when its exact server address is entered manually.
- Opens the receiver's official client inside KiwiDX and carries over the selected frequency and mode without adding the server to defaults or discovery lists.

## 0.1.47

- New waterfall rows now enter at the top and flow downward through the display.
- Changed the two passband edge lines from green to high-contrast white and increased their thickness to 2.5 pixels.

## 0.1.46

- Aligned the KiwiSDR WebSocket authentication command with the current official client format.
- Receive loops now start before authentication so server replies and rejection details are handled immediately.
- Removed non-protocol startup messages that could cause some KiwiSDR proxy nodes to reject the session.

## 0.1.45

- The 15-second missing-audio timeout now applies only while Play is active.
- Pressing Play starts a fresh 15-second audio-response window.
- Waterfall responsiveness continues to be monitored independently while connected.

## 0.1.44

- Waterfall MP4 recordings now include synchronized receiver audio.
- Audio is captured from the decoded KiwiSDR PCM stream and muxed as 128 kbps AAC while preserving the H.264 video stream.

## 0.1.43

- Changed the default waterfall range to -120 dB minimum and -50 dB maximum.
- Added editable WF minimum and maximum values to Settings > Startup and `startup.json`.
- Rebuilt the Windows application icon from the updated `icon.png`.

## 0.1.42

- Moved recording controls into a dedicated Recording section.
- Added Record Waterfall mode for capturing the waterfall, dial, bookmarks, bands, and frequency scale as 854x480 MP4 video.
- Bundled FFmpeg for self-contained H.264 MP4 encoding without a separate installation.
- The same Record button now saves MP3 audio or MP4 waterfall video according to the checkbox.

## 0.1.41

- Added a toggle recording control that captures the decoded KiwiSDR PCM stream.
- Stopping opens a Save As dialog and encodes the recording as a 128 kbps MP3.
- Suggested filenames include frequency in kHz, mode, and timestamp: `Record_Frequency_Mode_MMDDYY_HH-mm-ss.mp3`.
- Recordings are resampled from 12 kHz mono to 44.1 kHz mono for broad MP3 encoder compatibility.

## 0.1.40

- Grouped the BW Hz label and bandwidth input so they always remain on the same UI line.

## 0.1.39

- Frequency bookmarks are now assigned to one or more KiwiSDR servers instead of being global.
- The bookmark editor includes a checked server list containing Favorites and the current receiver.
- The waterfall and My Bookmarks menu only show bookmarks assigned to the current server.
- Existing unassigned bookmarks are migrated to the server selected when KiwiDX first loads them.

## 0.1.38

- Fixed a crash when selecting Add Bookmark from the dial context menu.
- The context menu now remains alive while WinForms completes click processing and is disposed with the waterfall control.

## 0.1.37

- Added persistent frequency bookmarks stored in `bookmarks.json`.
- Right-clicking the tuned dial marker now offers Add Bookmark.
- Added a 72-color palette plus a custom RGB color picker, 10-character names, and 30-character tooltip descriptions.
- Visible bookmarks appear below the frequency scale at the top of the waterfall without covering it.
- Added Bookmarks > My Bookmarks with direct tuning and Manage Bookmarks for editing, hiding/showing, and deleting entries.

## 0.1.36

- Added connection timeouts and real audio/waterfall readiness confirmation before reporting Connected.
- Detects KiwiSDR `too_busy`, `down`, and relevant `badp` rejection responses with user-friendly notifications.
- Added a 15-second audio/waterfall watchdog for connections that stop responding.
- Lost or rejected sessions are cleaned up and the UI returns to Connect/Play state.

## 0.1.35

- Receiver changes from the map now stop audio and fully disconnect the active SDR before connecting the selected one.
- Serialized connection operations prevent overlapping socket/audio initialization and duplicate map clicks.
- Failed connection attempts now clean up partially opened resources before another receiver is selected.
- Favorite receiver changes use the same safe switching operation.

## 0.1.34

- Zoom changes now preserve and reproject the existing waterfall history instead of clearing it.
- Zooming in enlarges the existing tuned region; zooming out preserves known data and leaves only previously unseen spectrum empty until new lines arrive.
- Incoming waterfall lines continue seamlessly after a zoom transition.

## 0.1.33

- Added a dynamic kHz frequency scale across the top of the waterfall.
- Expanded the bottom band map to include all listed amateur and broadcast bands.
- Sharpened band markers with defined outlines and highlighted every band containing the tuned frequency.

## 0.1.32

- Added Settings > Startup for selecting a startup server from Favorites.
- Added startup frequency, automatic connection, and automatic audio options.
- Startup preferences are persisted in `startup.json`.
- KiwiDX falls back to its default server when no favorites exist.

## 0.1.31

- KiwiDX now detects the tuned frequency band after connecting and preselects it in the Band list.
- Automatic band selection does not alter the current frequency, mode, bandwidth, or zoom.
- Overlapping band ranges are resolved using the nearest band center.

## 0.1.30

- Zoom now centers the waterfall directly on the tuned dial frequency.
- Moved the Zoom − and Zoom + buttons into a compact row below the Zoom slider.

## 0.1.29

- Zoom now anchors on the tuned dial frequency and preserves its horizontal cursor position.
- Old waterfall rows are cleared when changing scale so data from different zoom levels is not mixed.
- Added the active zoom level and visible span directly to the waterfall overlay.
- Made the actual tuned-frequency cursor yellow and labeled it for better visibility.

## 0.1.28

- Replaced the separate Play and Stop controls with one audio toggle button.
- The button is green while stopped (`Play`) and red while audio is playing (`Stop`).
- Disconnecting resets the audio control to its Play state.

## 0.1.27

- Fixed the Favorites star glyph being clipped by centering a smaller symbol-compatible font.
- Corrected the About window year from 2016 to 2026.

## 0.1.26

- Restyled the Favorites star as a borderless icon aligned with the Server text field.
- Matched the star control height and vertical margins to the Server text field.

## 0.1.25

- The star button now indicates whether the current server is bookmarked.
- Clicking a filled star removes that server from `favorites.json` and the Favorites menu.

## 0.1.24

- Added portable JSON storage for favorite KiwiSDR servers.
- Added a star button beside the Server field to bookmark the current receiver using its station title and location.
- Added File, Bookmarks, and Help menus with automatic favorite connections, Exit, and About commands.
- Added an About window with the application icon, version, author, and contact information.

## 0.1.23

- The receiver map window now closes automatically after a valid station is selected.
- Automatic connection continues in the main KiwiDX window.

## 0.1.22

- Added an embedded receiver map powered by rx.linkfanel.net.
- Clicking a receiver name transfers its URL to KiwiDX and connects automatically.
- Receiver links are intercepted inside the application instead of opening an external browser.

## 0.1.21

- Added a bold green `Server:` label beside the server URL field.

## 0.1.20

- Server Info field labels are now bold and green.
- Server details are displayed one field per line for easier scanning.

## 0.1.19

- Fixed Console Log layout when toggling its checkbox.
- Console Log and Server Info now use visible, docked containers above the bottom status bar.
- Console Log expanded to 150 pixels when enabled.

## 0.1.18

- Application renamed from KiwiKonnectSharp to KiwiDX.
- Project folder, project file, namespace, assembly and SDR client identity renamed.
- Custom application and window icon generated from `icon.svg`.

## 0.1.17

- Complete US English translation of the interface and runtime messages.
- Optional Console Log panel, hidden unless enabled by its checkbox.
- Optional Server Info panel showing the server URL, port, station message, antenna, location, hardware, software, coverage, users, SNR and GPS information.

## 0.1.16

- Navegador del espectro reubicado debajo del waterfall, alineado con su eje.
- Etiquetas dinámicas de frecuencia central y rango visible.
- Botones laterales para desplazar la vista un 20% del tramo visible.
- Zoom y navegación agrupados en un único módulo compacto.

## 0.1.15

- Slider de zoom continuo por niveles KiwiSDR `0–14`.
- Barra horizontal para recorrer todo el espectro de 0 a 30 MHz.
- Controles de dial integrados en una sola línea dentro del waterfall.
- Botones negativos y positivos con colores contrastantes diferenciados.

## 0.1.14

- Renderizado determinista del waterfall mediante búfer ARGB y `LockBits`.
- Verificación directa de filas W/F de 1024 bins contra el servidor configurado.
- Controles de dial `---`, `--`, `-`, `+`, `++` y `+++` para pasos de 100, 10 y 1 kHz.

## 0.1.13

- Contenedor de diseño explícito que reserva siempre el área central del waterfall.
- Identificador WebSocket compatible con el entero de 32 bits esperado por KiwiSDR.
- Contador visible de filas y bins recibidos del waterfall.
- Selector de bandas de radiodifusión y radioaficionado con sintonía automática.

## 0.1.12

- Sincronización real del zoom y desplazamiento del waterfall con KiwiSDR.
- Sintonía atómica de frecuencia, modo y filtro de audio.
- Ajuste del bandwidth mediante las líneas laterales verdes.
- Paleta KiwiSDR basada en dBm y controles WF min/max.
- Corrección del comando de interpolación del waterfall.
- Distribución adaptable de los controles al tamaño de la ventana.
- Serialización de comandos WebSocket y registro de comandos/respuestas.

## 0.1.11

- Versión base de KiwiKonnectSharp revisada.
