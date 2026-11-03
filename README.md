# VAK

A glass "dynamic island" for Windows that sits at the top-center of your screen,
collapsed until you hover it. Built with Electron. Named for Vikrant, Aditya and Kartik.

## Modules
- **Clipboard** - auto-captured copy history (text + images), click to copy back.
- **Focus** - Pomodoro timer with presets and a completion notification.
- **Weather** - current conditions for a saved city (Open-Meteo, no API key).
- **System** - live CPU, RAM, and battery.
- **World Clock** - local time plus any cities you add.
- **Units** - length, mass, volume, speed, data conversion.
- **Claude / ChatGPT chat** - embedded browser views, signed in with your own
  account (session cookies are stored locally, same as a normal browser).
- **File Shelf** - drag files onto the island to keep them one hover away;
  drag them back out into any app.
- **Converter** - image format conversion (PNG/JPEG/WEBP) and quick ZIP
  compression. DOCX -> PDF needs LibreOffice installed (`soffice` on PATH).

## Getting started
```powershell
./scripts/setup.ps1   # first time only - installs dependencies
./scripts/run.ps1      # start VAK
```

Hover the top-center of your screen, or press `Ctrl+Alt+Space`, to expand it.

## Project layout
```
electron/     main process - window, tray, hotkey, store, clipboard watcher, system stats, file + convert IPC handlers
src/          renderer - UI, styles, per-module scripts
scripts/      setup.ps1 / run.ps1
```

## Status
See the commit history for current phase. Roadmap:
1. Shell, hover expand/collapse, glass UI, clock - **done**
2. Theme pass (black panel, flat cards, circular dock) - **done**
3. Clipboard, Focus timer, Weather, System, World Clock, Units - **done**
4. Chat webviews (Claude + ChatGPT), File Shelf, Converter - **done (basic)**
5. Remaining widgets (Notes, Links, Message banner) - pending
6. Packaging as a signed installer, auto-start polish - pending
