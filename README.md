# Isla

A glass "dynamic island" for Windows that sits at the top-center of your screen,
collapsed until you hover it. Built with Electron.

## Modules (v1)
- **Clock** - always visible in the collapsed pill.
- **Claude / ChatGPT chat** - embedded browser views, signed in with your own
  account (session cookies are stored locally, same as a normal browser).
- **File Shelf** - drag files onto the island to keep them one hover away;
  drag them back out into any app.
- **Converter** - image format conversion (PNG/JPEG/WEBP) and quick ZIP
  compression. DOCX -> PDF needs LibreOffice installed (`soffice` on PATH).

## Getting started
```powershell
./scripts/setup.ps1   # first time only - installs dependencies
./scripts/run.ps1      # start Isla
```

Hover the top-center of your screen, or press `Ctrl+Alt+Space`, to expand it.

## Project layout
```
electron/     main process - window, tray, hotkey, file + convert IPC handlers
src/          renderer - UI, styles, per-module scripts
scripts/      setup.ps1 / run.ps1
```

## Status
See the project board / commit history for current phase. Roadmap:
1. Shell, hover expand/collapse, glass UI, clock - **done**
2. File Shelf drag-in/out - **done (basic)**
3. Chat webviews (Claude + ChatGPT) - **done (basic)**
4. Converter (image + zip) - **done (basic)**, DOCX/PDF/video next
5. Wallpaper-adaptive accent color - **done**
6. Packaging as a signed installer, auto-start polish - pending
