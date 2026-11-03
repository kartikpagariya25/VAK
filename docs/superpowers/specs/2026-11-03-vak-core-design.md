# VAK Core: Design Spec

Date: 2026-11-03
Status: draft for review

## 1. Goal

VAK is an original Windows "notch" app: a black pill at the top-center of the
screen that expands on hover into a panel of widgets. It is a native WPF
(.NET 8) app. Its look and behavior follow the Microsoft Store app Vayli
(Modi Studio) as observed from screenshots and static/UI inspection, but all
code, the logo, the mascot and the icon set are original to VAK.

This spec covers build 1, the "Core 6": notch shell, header and dock,
Home, System, Clock, Timers, plus Settings. The remaining pages (Weather,
Units, Clipboard, Notes, Links, File Shelf, Screen Time, Message, Drop,
AI Coding) are later builds and plug into the same widget contract.

The existing Electron prototype in `electron/` and `src/` is superseded.
It is removed in the first implementation step.

## 2. Evidence and boundaries

Observed (REA static inspection of `Vayli.exe` 1.1.60, Windows UI Automation of
the running app, and the user's screenshots):

- Vayli is a WPF/.NET Framework desktop app; Drop is a separate ASP.NET Core
  host. It uses Inter and Geist fonts (OFL), Fluent System Icons and Fluent
  Emoji (MIT).
- Expanded panel is 800 x 388 DIP at the top-center. Collapsed it is a black
  pill showing the time and battery.
- Home is a 3 x 2 grid. The dock has 14 round buttons, about 37 DIP wide on a
  pitch of about 46 DIP, plus an Add-to-dock picker with search.
- Sampled colors: panel `#000000`, card `#0D0D0F`, inner control `#2A2A2C`,
  accent `#2F7CF6`, primary button `#1D64D8`, active dock button white.

Not observed: exact corner radii and animation timings. These are tuned by eye
against the screenshots during the build.

Boundaries: no Vayli code, sprite art, logo or license/trial logic is copied.
Fonts (Inter, Geist) and icons (Fluent System Icons) are reused under their
OFL and MIT licenses, with the license texts shipped.

## 3. Architecture

```
VAK.sln
  src/VAK.App            WPF app: shell window, header, dock, design system, tray
  src/VAK.Core           widget contract, services, settings; no UI
  tests/VAK.Core.Tests   xUnit
```

- MVVM with CommunityToolkit.Mvvm.
- Each widget implements `IWidget`: id, title, subtitle, icon, home card view,
  full page view. A registry lists all widgets. The Home grid and the dock both
  read the registry; Add-to-dock is a view over it.
- Services live in Core behind interfaces: battery (WinRT), network and
  CPU/RAM/disk (.NET APIs and performance counters), volume and output device
  (NAudio on Core Audio), time zones (`TimeZoneInfo`), timer engine (no UI).
- Settings are JSON in `%AppData%\VAK\settings.json`. Nothing leaves the
  machine in build 1.

## 4. Visual system

- One XAML resource dictionary holds color, radius, spacing and type tokens
  using the sampled colors above.
- Header: VAK logo badge, page title, subtitle; right side battery, pin/theme
  button, settings, close.
- Cards are flat `#0D0D0F` with no borders or shadows.
- Pill-to-panel growth animates width and height with easing, about 250 ms.
  Page switches use a short fade.
- Brand: original VAK logo and mascot, drawn as vectors. Icons from Fluent
  System Icons.

## 5. Shell behavior

- One borderless, transparent, topmost window centered on the primary display.
- Expand when the cursor enters the notch area or on the global hotkey
  (default `Ctrl+Alt+Space`). Collapse shortly after the cursor leaves, with a
  small delay to avoid flicker.
- Collapsed: clicks pass through everywhere except the pill.
- Hides over fullscreen apps. Follows the primary display. (Assumption, to be
  confirmed against Vayli during the build.)
- Tray icon: Show/Hide, Start with Windows, Quit.

## 6. Core 6 pages

- **Home:** 3 x 2 grid of slots. Edit mode assigns a widget to each slot; an
  empty slot shows "+ Add widget". Build 1 offers System, Clock and Timers
  cards. Layout is saved in settings.
- **System:** Battery (percent, charging state, health from full-charge versus
  design capacity via WMI); Network (Wi-Fi name, link speed, up/down KB/s with
  sparklines); Performance (CPU, RAM, disk with sparklines); Volume (slider,
  mute, output device picker).
- **Clock:** digital or analog; 12/24-hour; seconds on/off; date on/off; date
  style Full/Short/Compact; world clocks with add-city and offset labels such
  as "-10h 30m".
- **Timers:** Pomodoro (focus, break, long break, cycles, auto-start next,
  completed today); Countdown; Stopwatch; Water counter (0/8).
- **Settings:** start with Windows, hotkey, default 12/24-hour, theme. A small
  page, not a full settings app.

## 7. Error handling

- Any data-source failure shows "-" on its card. No crash, no dialog.
- A corrupt settings file falls back to defaults and the bad file is kept as
  `settings.json.bak`.
- Services sit behind interfaces so one failing service never blocks the shell.

## 8. Testing

- xUnit unit tests: timer state machine (Pomodoro cycles, pause/resume,
  auto-start), time-zone offset text, settings load and fallback.
- Manual: run the app and compare each page with the screenshots.
- Not in scope: pixel-exact UI automation.

## 9. Prerequisites and risks

- A .NET 8 SDK is required and is not installed on the dev PC. Installing it
  (for example through winget) needs the user's approval.
- Removing the Electron prototype is deliberate and happens in the first step.
- Animation timings and corner radii are estimates until tuned in the build.
- Battery health and Wi-Fi name depend on hardware and drivers; on a desktop
  without a battery the Battery card shows "-".
