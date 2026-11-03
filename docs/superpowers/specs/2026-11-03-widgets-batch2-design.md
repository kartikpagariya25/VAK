# VAK Widgets, Batch 2: Design Spec

Date: 2026-11-03
Status: approved by the user's instruction to build the remaining widgets ("go for the next widgets", testing happens after all development)
Builds on: `2026-11-03-vak-core-design.md` (architecture, design tokens, widget registry, home grid, dock).

## Goal
Add the remaining ten widgets so VAK covers every page in `docs/reference/vayli`. Each is a `WidgetModule` (page, optional Home card) registered in `WidgetCatalog`; logic with no UI lives in `VAK.Core` and is tested first.

## Widgets and data sources
| Widget | Behavior | Data / side effects |
|---|---|---|
| Units | Length, Mass, Temperature, Data, Volume, Speed, Time, Currency; from/to with swap, other-unit list, recent | Offline, except Currency rates from api.frankfurter.app (cached, optional) |
| Weather | Current, hourly, 5-day, city search, C/F | Open-Meteo forecast and geocoding APIs (no key); default city from settings |
| Notes | List, search, create, edit, delete; autosave | `%AppData%\VAK\notes.json` |
| Links | Search box with engine chips, saved links, add link | `%AppData%\VAK\links.json`; opens the default browser |
| Clipboard | History of text, images, links with tabs, search, pin, delete, copy back | Clipboard listener; `%AppData%\VAK\clipboard\` |
| Screen Time | Today total, per-app list, 7-day bars, daily limit | Foreground-window hook; `%AppData%\VAK\screentime.json` |
| Message | Flash a short banner on the notch with animation, colour, duration | In memory |
| File Shelf | Parked files on shelves; drop in, drag out, copy all, clear | Copies under `%AppData%\VAK\shelf\` |
| Drop | Send files between devices on the same private network via a local web page and QR code | Local HTTP listener on the private LAN only |
| AI Coding | Today's tokens, messages, tool calls, sessions, active hours, per-agent usage for Claude Code and Codex | Reads local usage logs (`~/.claude/projects`, `~/.codex`); read-only; nothing leaves the machine |

## Rules
- Same visual system as build 1: tokens, flat cards, Inter/Geist, Fluent icons.
- Any failing source shows "-" or an empty state, never a dialog or crash.
- All persistence goes through atomic JSON writes that swallow IO/permission errors.
- Network use is limited to Weather and Currency; Drop listens on the private LAN only and refuses public networks.
- No Vayli code, art or logo is copied; only layout and behavior are matched.

## Order
Units, Weather, Notes, Links, Clipboard, Screen Time, Message, File Shelf, Drop, AI Coding. Each widget is committed and pushed separately.

## Testing
Pure logic (conversion, formatting, parsing, aggregation, search/filter) is test-first in `VAK.Core.Tests`. UI is checked by running the app against the reference screenshots; the user will do the full app test once all widgets exist.
