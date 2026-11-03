# VAK

A notch for Windows: a black pill at the top-center of your screen that expands on hover into a panel of widgets. Native WPF on .NET 8. Named for Vikrant, Aditya and Kartik.

## Run
```powershell
dotnet run --project src/VAK.App
```
Requires the .NET 8 SDK. Hover the top-center of the screen, or press `Ctrl+Alt+Space`.

## Layout
```
src/VAK.App            WPF app: shell, design system, pages
src/VAK.Core           settings, widget registry, timer/clock/system logic (no UI)
tests/VAK.Core.Tests   xUnit tests
docs/superpowers       design spec and implementation plan
docs/reference/vayli   reference screenshots used for the visual pass
```

## Status
Build 1 (in progress): shell, Home, System, Clock, Timers, Settings. Later: Weather, Units, Clipboard, Notes, Links, File Shelf, Screen Time, Message, Drop, AI Coding.
