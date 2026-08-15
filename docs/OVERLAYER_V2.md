# Overlayer v2 preview

`overlayer-v2.cs` is the first CRNTLY modernization pass. It is intentionally kept alongside the original `overlayer.cs` so the working WinForms version remains available while the WPF/DLL path is tested inside Streamer.bot.

## Goals

- Keep the core promise: **many overlays through one OBS Browser Source**.
- Move UI/window lifecycle into `CRNTLY.StreamerBot.UI.dll`.
- Stop using WinForms controls as backend state.
- Keep the existing `overlayer/listview.json` data readable and mostly backward-compatible.
- Remove the directory-wide local-file reads and filename-collision map from v1.
- Stream local assets instead of allocating the entire file in memory.
- Let the OBS compositor update while it stays loaded instead of requiring the whole browser page to be rebuilt.

## Runtime layout

```text
Streamer.bot
  overlayer-v2.cs
    |-- config store
    |-- CompositeOverlayServer
    |     |-- localhost:42069/       compositor shell
    |     |-- localhost:42069/state  tiny live state payload
    |     `-- localhost:42070/local/<overlay-id>/... local assets
    |
    `-- CRNTLY.StreamerBot.UI.dll
          `-- WPF Overlayer window

OBS
  ONE Browser Source -> http://localhost:42069/
```

The compositor shell polls `/state` every 1.5 seconds. It only mutates iframe DOM when the state payload actually changes, preserving loaded overlays between normal polls.

## Local files

A `file:///.../overlay.html` source receives an isolated local route based on its overlay ID. Relative files such as `css/style.css`, `js/app.js`, images, fonts, and media resolve under that overlay's directory without scanning the whole directory or mixing files from another overlay with the same filename.

Local files are served with a 64 KiB streaming buffer and `FileShare.ReadWrite` so development tools can update assets while OBS is using them.

## Iframe compatibility

v2 currently keeps **direct iframe** behavior for remote URLs. Some websites deliberately block framing with CSP `frame-ancestors` or `X-Frame-Options`; this first pass does not pretend those restrictions can be universally bypassed.

A later compatibility mode can be added behind a per-overlay renderer option, but it should preserve the one-Browser-Source model and be tested against real overlay providers before becoming default behavior.

## Testing checklist

1. Build `CRNTLY.StreamerBot.UI.dll` on Windows.
2. Add the DLL and `Newtonsoft.Json.dll` as Streamer.bot C# references.
3. Paste/compile `overlayer-v2.cs` in an Execute C# Code sub-action.
4. Run the action and confirm the WPF window opens without blocking the action queue.
5. Confirm existing `overlayer/listview.json` entries appear.
6. Start the server and add one OBS Browser Source at `http://localhost:42069/`.
7. Test remote overlays, enable/disable, ordering, and edits.
8. Test a local HTML overlay with relative CSS/JS/image assets.
9. Close/reopen the WPF window and verify the server can remain owned by the script lifecycle.
10. Shut down/recompile Streamer.bot and confirm `Dispose()` releases ports 42069/42070.

## Not yet claimed complete

- compatibility proxy for iframe-hostile sites
- drag-and-drop ordering (up/down is implemented first)
- packaged DLL distribution/update mechanism
- provider-specific compatibility testing
- automated Windows/WPF build CI
