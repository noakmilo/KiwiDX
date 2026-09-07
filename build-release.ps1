param(
    [string]$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string]$FfmpegPath = "$PSScriptRoot\ffmpeg.exe"
)
$ErrorActionPreference = 'Stop'
[xml]$project = Get-Content "$PSScriptRoot\KiwiDX.csproj"
$version = [string]$project.Project.PropertyGroup.Version
$dist = Join-Path $PSScriptRoot 'dist'
# A fresh directory prevents stale executables or local data entering a release.
$publish = Join-Path $dist ("publish-" + [guid]::NewGuid().ToString('N'))
if (!(Test-Path -LiteralPath $IsccPath)) { throw 'Install Inno Setup 6 or pass -IsccPath.' }
if (!(Test-Path -LiteralPath $FfmpegPath)) { throw 'Pass -FfmpegPath pointing to the bundled ffmpeg.exe from a previous KiwiDX release.' }
New-Item -ItemType Directory -Force $publish | Out-Null
dotnet publish "$PSScriptRoot\KiwiDX.csproj" -c Release -r win-x64 --self-contained true -p:AssemblyName=KiwiDX -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Copy-Item -LiteralPath $FfmpegPath -Destination (Join-Path $publish 'ffmpeg.exe')
Copy-Item -LiteralPath "$PSScriptRoot\dependences.txt" -Destination $publish
& $IsccPath "/DAppVersion=$version" "/DPublishDir=$publish" "$PSScriptRoot\installer\KiwiDX.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$zip = Join-Path $dist "KiwiDX_v${version}_Portable_Windows_x64.zip"
Compress-Archive -Path "$publish\*" -DestinationPath $zip -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $zip, (Join-Path $dist "KiwiDX_v${version}_Setup_x64.exe") |
    ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } |
    Set-Content (Join-Path $dist "KiwiDX_v${version}_SHA256SUMS.txt")
