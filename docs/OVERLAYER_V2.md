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
- Let the Streamer.bot action compile even when the optional CRNTLY UI DLL is not installed yet.

## Runtime layout

```text
Streamer.bot
  overlayer-v2.cs
    |-- local bootstrap / reflection proxy
    |-- config store
    |-- CompositeOverlayServer
    |     |-- localhost:42069/       compositor shell
    |     |-- localhost:42069/state  tiny live state payload
    |     `-- localhost:42070/local/<overlay-id>/... local assets
    |
    `-- dlls/CRNTLY.StreamerBot.UI.dll
          |-- OverlayerScriptBridge
          `-- WPF Overlayer window

OBS
  ONE Browser Source -> http://localhost:42069/
```

The compositor shell polls `/state` every 1.5 seconds. It only mutates iframe DOM when the state payload actually changes, preserving loaded overlays between normal polls.

## Local DLL bootstrap

`overlayer-v2.cs` contains no compile-time CRNTLY type references. It looks for:

```text
<Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
```

and loads `Crntly.StreamerBot.UI.Overlayer.OverlayerScriptBridge` dynamically. The bridge exchanges JSON payloads and ordinary `Action` callbacks with the script, while keeping the real WPF implementation strongly typed inside the DLL.

For the current local test phase, if the DLL is missing the action can still compile and run far enough to display a simple bootstrap dialog explaining where the file was expected and telling the tester to run:

```powershell
.\build-ui.ps1
```

The build script deploys the DLL to Streamer.bot's `dlls` directory when it can locate the installation. The future livestreaming.tools downloader can replace this missing-component branch without changing the Overlayer runtime/UI contract.

## Local files

A `file:///.../overlay.html` source receives an isolated local route based on its overlay ID. Relative files such as `css/style.css`, `js/app.js`, images, fonts, and media resolve under that overlay's directory without scanning the whole directory or mixing files from another overlay with the same filename.

Local files are served with a 64 KiB streaming buffer and `FileShare.ReadWrite` so development tools can update assets while OBS is using them.

## Iframe compatibility

v2 currently keeps **direct iframe** behavior for remote URLs. Some websites deliberately block framing with CSP `frame-ancestors` or `X-Frame-Options`; this first pass does not pretend those restrictions can be universally bypassed.

A later compatibility mode can be added behind a per-overlay renderer option, but it should preserve the one-Browser-Source model and be tested against real overlay providers before becoming default behavior.

## Testing checklist

1. Pull the latest repo.
2. Run `build-ui.ps1` on Windows and confirm it deploys `CRNTLY.StreamerBot.UI.dll` into Streamer.bot's `dlls` directory.
3. In Streamer.bot, `overlayer-v2.cs` should only need `Newtonsoft.Json.dll` as its project-specific C# reference.
4. Optional bootstrap test: temporarily rename/remove `CRNTLY.StreamerBot.UI.dll`, run the action, and confirm the missing-component dialog appears instead of a compile failure. Restore/rebuild the DLL afterward.
5. Paste/compile `overlayer-v2.cs` in an Execute C# Code sub-action.
6. Run the action and confirm the WPF window opens without blocking the action queue.
7. Confirm existing `overlayer/listview.json` entries appear.
8. Start the server and add one OBS Browser Source at `http://localhost:42069/`.
9. Test remote overlays, enable/disable, ordering, and edits.
10. Test a local HTML overlay with relative CSS/JS/image assets.
11. Close/reopen the WPF window and verify the server can remain owned by the script lifecycle.
12. Shut down/recompile Streamer.bot and confirm `Dispose()` releases ports 42069/42070.

## Not yet claimed complete

- automatic download/update from livestreaming.tools
- compatibility proxy for iframe-hostile sites
- drag-and-drop ordering (up/down is implemented first)
- provider-specific compatibility testing
- automated Windows/WPF build CI
