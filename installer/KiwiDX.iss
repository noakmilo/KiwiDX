#ifndef AppVersion
  #error AppVersion must be supplied by build-release.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by build-release.ps1
#endif

[Setup]
AppId={{C5F902C8-65AE-4B20-89A4-E9962D4D69BD}
AppName=KiwiDX
AppVersion={#AppVersion}
AppPublisher=Kmilo Noa
AppPublisherURL=https://github.com/noakmilo/KiwiDX
AppUpdatesURL=https://github.com/noakmilo/KiwiDX/releases/latest
DefaultDirName={localappdata}\Programs\KiwiDX
DefaultGroupName=KiwiDX
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\KiwiDX.exe
SetupIconFile=..\icon.ico
OutputDir=..\dist
OutputBaseFilename=KiwiDX_v{#AppVersion}_Setup_x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#FileVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; User data is deliberately excluded and is never removed by the uninstaller.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "favorites.json,bookmarks.json,startup.json,*.bak,*.pdb,*.WebView2\*"

[Icons]
Name: "{group}\KiwiDX"; Filename: "{app}\KiwiDX.exe"
Name: "{autodesktop}\KiwiDX"; Filename: "{app}\KiwiDX.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\KiwiDX.exe"; Description: "Launch KiwiDX"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  InstalledPacked, SetupPacked: Int64;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C5F902C8-65AE-4B20-89A4-E9962D4D69BD}_is1', 'DisplayVersion', InstalledVersion) then
    if StrToVersion(InstalledVersion, InstalledPacked) and StrToVersion('{#AppVersion}', SetupPacked) then
    if ComparePackedVersion(InstalledPacked, SetupPacked) > 0 then
    begin
      MsgBox('A newer version of KiwiDX is already installed. Setup will exit without changing your installation.', mbInformation, MB_OK);
      Result := False;
    end;
end;
