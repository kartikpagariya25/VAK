# VAK: AI Chat, Calendar, File Converter (Batch 3)

Date: 2026-11-04
Status: scoped with the user's answers; implementation follows in the order below.
Builds on: `2026-11-03-vak-core-design.md` and `2026-11-03-widgets-batch2-design.md`.

## What changes from batch 2
- The current "AI Coding" page (token counts and dollar estimates from local Claude Code / Codex logs) is **removed**. It was a misreading of the request.
- "AI Coding" becomes a **chat page** where the user prompts Claude and GPT from the island and reads the answers there.
- New widgets: **Calendar** and **File Converter**.
- **File Shelf** gets real UI testing (drag in, drag out, paste, copy all, select, clear), and fixes for whatever that finds.

## 1. AI Coding (chat)
The page has two providers, **Claude** and **GPT**, and each can run in two modes.

### Website mode (default)
- The real claude.ai / chatgpt.com site is shown inside VAK with Microsoft WebView2. The user signs in on the real site once; the session cookies live in VAK's own profile folder (`%AppData%\VAK\webview`) and survive restarts.
- VAK never sees or stores the password; there is no "redirect and token" flow because neither company offers third-party apps a way to use a consumer subscription login.
- **Known risk to resolve first (spike):** the island window is a transparent layered window, and normal WebView2 does not render inside those. The spike tries `WebView2CompositionControl`; if it fails, the website view opens in a borderless companion window docked under the island.

### Native mode (API key)
- Settings gets one API key field per provider. Keys are stored encrypted with Windows DPAPI (current user), never in plain text and never logged.
- Anthropic Messages API and OpenAI Chat Completions API, both streamed (server-sent events), so words appear as they are written.
- A model picker per provider (editable list with sensible defaults, plus a free-text model id).
- **Saved conversations:** a list of chats stored locally (`chats.json`), with new, rename, delete and reopen.
- **Attachments:** drop or paste images and text/code files into the prompt. Images are sent as image parts; text files are inlined with their name.
- **Markdown and code blocks:** headings, lists, bold/italic, inline code and fenced code blocks with a Copy button.
- Network calls go only to `api.anthropic.com` and `api.openai.com`, only when the user sends a message.
- Errors (bad key, rate limit, offline) show as a message in the chat, never a dialog.

### Layout
- The page asks the island for a **taller panel (about 800 x 560)** while it is open, and the island returns to 800 x 388 on other pages.
- Header: provider switch (Claude | GPT) and mode switch (Website | API).

## 2. Calendar
- Month view with today highlighted, and an agenda list for the selected day. Local only: events live in `%AppData%\VAK\calendar.json`; there is no sign-in and no sync.
- Event: title, date, start/end time or all-day, colour, optional note, repeat rule.
- **Repeats:** daily, weekly, monthly, yearly, with an optional end date. Occurrences are computed, not stored.
- **Reminders:** a notification and a notch banner at a chosen lead time (none, at start, 5, 15, 30, 60 minutes); fired once per occurrence, survive restarts, and skipped for times already past at startup.
- **Colours and all-day events.**
- **Home card:** "Today" with the next few events.

## 3. File Converter
- Drop files or choose files; pick a target format; converted files go to a "VAK Converted" folder next to the original by default (never overwriting; names are numbered).
- **Images:** PNG, JPG, WEBP, BMP, GIF, TIFF, ICO; optional resize and quality.
- **Documents to PDF:** images to PDF; text, Markdown and HTML to PDF. Word and other Office files to PDF only when Word or LibreOffice is installed (the page says so when it is not).
- **Audio and video:** through the user's own **ffmpeg**. VAK looks for `ffmpeg.exe` on PATH (and in a few usual folders); if it is missing the page explains how to install it and re-checks. VAK never downloads ffmpeg itself.
- **Archives:** zip files and folders, unzip archives.
- Conversion runs off the UI thread with per-file progress and a Cancel button; a failed file shows its error and does not stop the rest.

## Rules that apply to all three
- Same design system and widget registry; each is a `WidgetModule`.
- Logic with no UI lives in `VAK.Core` and is tested first.
- Anything that can fail shows a message in place and never throws to the dispatcher.
- No Vayli code or art is copied; the Claude and OpenAI names are only used to label providers.

## Order of work
1. Remove the usage page and its code.
2. File Shelf: real UI tests and fixes.
3. File Converter.
4. Calendar.
5. AI chat: WebView2 spike, then website mode, then native mode.

## Testing
Pure logic (conversion planning, naming, recurrence, reminders, SSE parsing, markdown parsing, key storage, chat history) is test-first in `VAK.Core.Tests`. The UI is run and checked against the app; the user does the full test at the end.
