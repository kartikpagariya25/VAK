# Builds a self-contained VAK (no .NET install needed on the target PC) and zips it to dist\VAK-<version>-win-x64.zip
# Usage:  powershell -ExecutionPolicy Bypass -File tools\publish.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$name = "VAK-$Version-win-x64"
$out = Join-Path $root "dist\$name"
$zip = Join-Path $root "dist\$name.zip"

if (Get-Process VAK -ErrorAction SilentlyContinue) { Write-Host "Quit VAK first (tray icon > Quit VAK) so the build can replace its files."; exit 1 }
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

dotnet publish (Join-Path $root "src\VAK.App") -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -p:PublishReadyToRun=false -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

@"
VAK $Version - a notch for your Windows desktop

INSTALL
1. Unzip this folder anywhere you like (for example C:\Users\<you>\VAK).
2. Double-click VAK.exe. A black notch appears at the top centre of the screen.
3. Hover the notch or press Alt+V / Ctrl+Alt+Space to open it. The gear opens Settings.
4. In Settings > General turn on "Start VAK when Windows starts" if you want it every day.

TURN OFF
- The power button in the open panel hides the notch; the open shortcut brings it back.
- To quit completely: right-click the VAK tray icon > Quit VAK.

If Windows says "unknown publisher", choose More info > Run anyway (VAK is not code-signed).
AI chat in website mode needs the Microsoft Edge WebView2 runtime (built into Windows 11).
"@ | Set-Content (Join-Path $out "README.txt") -Encoding UTF8

Compress-Archive -Path "$out\*" -DestinationPath $zip -CompressionLevel Optimal
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Done: $zip ($mb MB)"
