# VAK

A notch for Windows: a black pill at the top-center of your screen that expands on hover into a panel of widgets. Native WPF on .NET 8. Named for Vikrant, Aditya and Kartik.

## Features
- **Notch shell** - collapsed pill showing the time and battery; hover the notch (or press the shortcut) to expand. Pin it open from the header. Hides over fullscreen apps. Message banners flash on the pill.
- **Home** - a 3 x 2 grid of cards. Use the pencil in the header to add, remove and undo.
- **System** - battery (charge, health), network (Wi-Fi name, speed, up/down), CPU / RAM / disk with sparklines, volume with mute and output picker.
- **Clipboard** - history of text, links and images (password-manager copies are skipped), search, pin, copy back.
- **Notes** - quick notes that save as you type, with search; a quick-add box on Home.
- **Timers** - Pomodoro, countdown, stopwatch, water counter.
- **File Shelf** - park file copies on shelves; drop in, paste with Ctrl+V, drag back out, copy all.
- **Drop** - send files between this PC and phones on a private network through a QR code (see below).
- **Weather** - current conditions, hourly and 5-day forecast from Open-Meteo, any city, C/F.
- **Clock** - digital or analog, 12/24-hour, seconds and date toggles, three date styles, world clocks.
- **Units** - length, mass, temperature, data, volume, speed, time and currency (live rates).
- **Links** - web search with engine chips and saved links.
- **Message** - flash an animated banner on the notch (six animations, colours, duration).
- **Screen Time** - per-app foreground time, daily limit, 7-day chart.
- **AI Coding** - today's tokens, messages, tool calls, sessions and an API-price estimate from your local Claude Code and Codex logs.
- **Settings** - start with Windows, open shortcut, time format.

## Privacy and network
- Everything is stored locally in `%AppData%\VAK`.
- The only internet calls are Weather (Open-Meteo) and Units currency rates (frankfurter.app).
- AI Coding reads `~/.claude/projects` and `~/.codex/sessions` read-only and keeps only counts, never message text.
- Drop only starts when you click Start, only on a private network (it refuses networks Windows marks Public), listens on the private LAN address only, and every request needs the random token in the QR code. Received files go to `Downloads\VAK Drop`. Windows may ask to allow VAK through the firewall: choose Private networks.

## Run
Requires the .NET 8 SDK.
```powershell
dotnet run --project src/VAK.App
```
The default shortcut is `Ctrl+Alt+Space`. If another app already uses it (the Claude desktop app does), VAK tells you and keeps working; change it under Settings.

Dev-only: `--gallery` opens the design gallery; `--left <dip>` pins the panel's left edge so VAK can run beside another notch app.

## Test
```powershell
dotnet test
```
Logic (settings, timers, clock, weather, units, notes, links, clipboard, screen time, banners, shelf, AI usage, Drop server) is unit- and integration-tested in `VAK.Core`. The UI is checked by running the app against the screenshots in `docs/reference/vayli`.

## Layout
```
src/VAK.App            WPF app: shell, design system, pages, cards, services
src/VAK.Core           settings, widget registry, and all logic (no UI)
tests/VAK.Core.Tests   xUnit tests
tools/make-icons.ps1   regenerates the .ico files from the brand mark
docs/superpowers       design specs and implementation plan
docs/reference/vayli   reference screenshots used for the visual pass
```

## Credits
Inter and Geist fonts (SIL OFL), Fluent System Icons (MIT) and QRCoder (MIT) are used under their licenses; font and icon licenses ship in `src/VAK.App/Assets/Fonts/LICENSES`. The VAK logo is original.

## Roadmap
Packaging as a signed installer, more themes, and a settings page for per-widget options.
