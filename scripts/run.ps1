# Isla - start the app.
$ErrorActionPreference = "Stop"
if (-not (Test-Path "node_modules\electron")) {
    Write-Host "Dependencies not installed yet. Running setup first..." -ForegroundColor Yellow
    & "$PSScriptRoot\setup.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
npm start
