# VAK - one-time setup. Run this from PowerShell in the project folder.
$ErrorActionPreference = "Stop"

Write-Host "Checking Node.js..." -ForegroundColor Cyan
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "Node.js not found. Install it from https://nodejs.org (LTS) and re-run this script." -ForegroundColor Red
    exit 1
}
node -v

Write-Host "Installing dependencies..." -ForegroundColor Cyan
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Host "npm install failed (see the errors above). Setup did NOT complete." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Native modules (sharp) and Electron's own .bin shim have to actually be
# present after install - a copy/move of node_modules between drives or
# tools can silently drop them without npm complaining.
$missing = @()
if (-not (Test-Path "node_modules\sharp")) { $missing += "sharp" }
if (-not (Test-Path "node_modules\electron\dist\electron.exe")) { $missing += "electron" }
if ($missing.Count -gt 0) {
    Write-Host "node_modules is incomplete (missing: $($missing -join ', '))." -ForegroundColor Red
    Write-Host "Run: Remove-Item -Recurse -Force node_modules, package-lock.json ; then re-run this script." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Setup complete. Run .\scripts\run.ps1 to start VAK." -ForegroundColor Green
