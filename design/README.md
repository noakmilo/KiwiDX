# KiwiDX v0.2.0 design

The approved generated reference is kiwidx-v0.2.0-reference.png. The application uses native WinForms controls; title-bar rendering follows Windows capabilities.

Icons are vector geometry in WorkspaceTheme.cs, not emoji or a system icon font. The PNG files in icons/ preview the same paths. The waveform ICO is used by the executable and installer.

Run the workspace checks from the repository root:

```powershell
dotnet run --project tests/Workspace/Workspace.Tests.csproj -c Release
```

The test suppresses startup network connections and renders synthetic receiver/chat data to dist/workspace-v0.2.0.png. This preview is not a live reception capture. Set KIWIDX_EXPORT_ICONS=1 when intentionally regenerating icon assets.
