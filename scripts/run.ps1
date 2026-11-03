# Isla - start the app.
$ErrorActionPreference = "Stop"
if (-not (Test-Path "node_modules")) {
    Write-Host "Dependencies not installed yet. Running setup first..." -ForegroundColor Yellow
    & "$PSScriptRoot\setup.ps1"
}
npm start
