# VAK

A notch for Windows: a black pill at the top-center of your screen that expands on hover into a panel of widgets. Native WPF on .NET 8. Named for Vikrant, Aditya and Kartik.

## Build 1 features
- **Notch shell** - collapsed pill showing the time and battery; hover the top-center of the screen (or press the shortcut) to expand. Pin it open from the header. Hides over fullscreen apps.
- **Home** - a 3 x 2 grid of cards. Use the pencil in the header to add, remove and undo.
- **System** - battery (charge, health), network (Wi-Fi name, speed, up/down), CPU / RAM / disk with sparklines, volume with mute and output picker.
- **Clock** - digital or analog, 12/24-hour, seconds and date toggles, three date styles, world clocks.
- **Timers** - Pomodoro (focus, break, long break, cycles, auto-start), countdown, stopwatch, water counter.
- **Settings** - start with Windows, open shortcut, time format.
- **Dock** - round buttons with an Add-to-dock picker.

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
Logic (settings, timers, clock formatting, hotkeys, home layout, startup) is unit-tested in `VAK.Core`. The UI is checked by running the app against the screenshots in `docs/reference/vayli`.

## Layout
```
src/VAK.App            WPF app: shell, design system, pages, cards
src/VAK.Core           settings, widget registry, timer/clock/system logic (no UI)
tests/VAK.Core.Tests   xUnit tests
tools/make-icons.ps1   regenerates the .ico files from the brand mark
docs/superpowers       design spec and implementation plan
docs/reference/vayli   reference screenshots used for the visual pass
```

## Credits
Inter and Geist fonts (SIL OFL) and Fluent System Icons (MIT) are bundled with their licenses in `src/VAK.App/Assets/Fonts/LICENSES`. The VAK logo is original.

## Roadmap
Next: Weather and Units, then Clipboard, Notes, Links, File Shelf and Screen Time, then Message, Drop and AI Coding. Packaging as a signed installer is also pending.
