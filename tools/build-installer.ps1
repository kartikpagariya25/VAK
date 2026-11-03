# Builds dist\VAK-Setup-<version>.exe with Inno Setup (runs publish.ps1 first if the folder is missing).
# One-time setup:  winget install JRSoftware.InnoSetup
# Usage:  powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) { Write-Host "Inno Setup is not installed. Run:  winget install JRSoftware.InnoSetup"; exit 1 }

if (-not (Test-Path (Join-Path $root "dist\VAK-$Version-win-x64\VAK.exe"))) {
    & (Join-Path $PSScriptRoot "publish.ps1") -Version $Version
}

& $iscc "/DAppVersion=$Version" (Join-Path $root "installer\VAK.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
Write-Host "Done: $(Join-Path $root "dist\VAK-Setup-$Version.exe")"
