# KiwiDX v0.1.50

- Import and export Favorites and Bookmarks from the Bookmarks menu.
- Imports merge without replacing existing entries; duplicate favorite URLs and bookmark IDs are skipped.
- User configuration is stored in `%LOCALAPPDATA%\KiwiDX`, with atomic saves and previous-file `.bak` backups.
- On first launch, select your old portable directory to migrate preferences, favorites and bookmarks. Existing profile files are preserved.
- Windows x64 Setup includes .NET 8 and FFmpeg. WebView2 Runtime remains a prerequisite for the map and embedded WebSDR.
- Install later Setup releases over the same installation. Uninstall preserves your data. Older installers are blocked when a newer version is already installed.
- Help > Check for Updates checks the latest stable GitHub release; download and installation are manual.

Packages: `KiwiDX_v0.1.50_Setup_x64.exe`, `KiwiDX_v0.1.50_Windows_x64.zip`, and SHA-256 checksums.

The installer is unsigned. Full install/upgrade/uninstall verification should be performed in a Windows test account before public release.
