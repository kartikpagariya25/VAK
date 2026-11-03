# Isla - one-time setup. Run this from PowerShell in the project folder.
$ErrorActionPreference = "Stop"

Write-Host "Checking Node.js..." -ForegroundColor Cyan
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "Node.js not found. Install it from https://nodejs.org (LTS) and re-run this script." -ForegroundColor Red
    exit 1
}
node -v

Write-Host "Installing dependencies..." -ForegroundColor Cyan
npm install

Write-Host ""
Write-Host "Setup complete. Run .\scripts\run.ps1 to start Isla." -ForegroundColor Green
