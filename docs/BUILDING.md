# Building, running and shipping VAK

Requires Windows 10/11 and the .NET 8 SDK.

## Run from source
```powershell
dotnet run --project src/VAK.App -c Release
```
Quit a running copy first (tray icon > Quit VAK) or `taskkill /IM VAK.exe /F`, otherwise the build cannot replace the exe.

## Test
```powershell
dotnet test -c Release
```

## Ship a zip (no .NET needed on the other PC)
```powershell
powershell -ExecutionPolicy Bypass -File tools\publish.ps1 -Version 1.0.0
```
Produces `dist\VAK-1.0.0-win-x64.zip` (self-contained, about 87 MB) with a short README. Unzip anywhere and run `VAK.exe`.

## Ship an installer
```powershell
winget install JRSoftware.InnoSetup
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1 -Version 1.0.0
```
Produces `dist\VAK-Setup-1.0.0.exe` (per-user install, Start menu shortcut, optional desktop shortcut and start with Windows, uninstaller).

## Notes
- Neither build is code-signed, so Windows SmartScreen may say "unknown publisher" (More info > Run anyway).
- AI chat in website mode needs the Microsoft Edge WebView2 runtime (built into Windows 11).
- ffmpeg (audio/video conversion) and Office (Word to PDF) are optional.
- User data lives in `%AppData%\VAK` and is not touched by updates or the uninstaller.
