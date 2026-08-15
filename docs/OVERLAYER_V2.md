# Overlayer v2 preview

`overlayer-v2.cs` modernizes the original WinForms Overlayer while preserving its core promise: **many overlays through one OBS Browser Source**.

The current architecture deliberately keeps the application in the Streamer.bot script and the reusable WPF machinery in one shared DLL.

## Goals

- Keep many overlays behind one OBS Browser Source.
- Keep the Overlayer layout and behavior in `overlayer-v2.cs`, where users expect the tool to live.
- Use `CRNTLY.StreamerBot.UI.dll` only for reusable WPF hosting, themes and controls.
- Stop using WinForms controls as backend state.
- Keep the existing `overlayer/listview.json` data readable and mostly backward-compatible.
- Isolate local-file routes per overlay.
- Stream local assets instead of allocating entire files in memory.
- Push compositor changes live while OBS keeps the Browser Source loaded.
- Let the Streamer.bot action compile even when the shared CRNTLY UI DLL is not installed yet.

## Runtime layout

```text
Streamer.bot
  overlayer-v2.cs
    |-- Overlayer XAML layout + UI behavior
    |-- local bootstrap / reflection proxy
    |-- config store
    |-- CompositeOverlayServer
    |     |-- localhost:42069/        compositor shell
    |     |-- localhost:42069/state  current state payload
    |     |-- localhost:42069/events SSE live updates
    |     `-- localhost:42070/local/<overlay-id>/... local assets
    |
    `-- dlls/CRNTLY.StreamerBot.UI.dll
          |-- generic CrntlyScriptWindowBridge
          |-- STA/WPF dispatcher host
          `-- shared CRNTLY themes + controls

OBS
  ONE Browser Source -> http://localhost:42069/
```

The compositor primarily receives changes through SSE. A 5-second `/state` poll remains as a reconciliation fallback. Explicit server shutdown sends a shutdown event and blank state so a loaded OBS Browser Source does not freeze the last rendered overlay.

## Why the layout lives in the script

The DLL is a shared component/runtime library, not an Overlayer application assembly.

```text
CRNTLY.StreamerBot.UI.dll owns:
  WPF dispatcher lifecycle
  script-window hosting
  theme tokens / palettes / density
  reusable buttons, icon buttons, inputs, toggles
  sliders, scrollbars, list rows, tooltips

Overlayer script owns:
  Overlayer window XAML
  field names and placement
  editing/autosave behavior
  reset buttons
  overlay ordering/deletion/duplication
  persistence
  compositor/server behavior
```

This keeps the install model to one common DLL plus one script and lets unrelated CRNTLY tools reuse the same visual system without carrying Overlayer classes inside the DLL.

## Local DLL bootstrap

`overlayer-v2.cs` contains no compile-time CRNTLY or WPF type references. It looks for:

```text
<Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
```

and dynamically loads:

```text
Crntly.StreamerBot.UI.ScriptHost.CrntlyScriptWindowBridge
```

The bridge accepts the script-owned XAML, resolves named controls and theme resources, forwards UI events, and performs WPF work on the shared STA dispatcher.

For the current local test phase, if the DLL is missing the action can still compile and run far enough to display a bootstrap dialog explaining where the file was expected and telling the tester to run:

```powershell
.\build-ui.ps1
```

## Local files

A `file:///.../overlay.html` source receives an isolated local route based on its overlay ID. Relative files such as `css/style.css`, `js/app.js`, images, fonts, and media resolve under that overlay's directory without scanning unrelated directories or mixing files from another overlay with the same filename.

Local files are served with a 64 KiB streaming buffer and `FileShare.ReadWrite` so development tools can update assets while OBS is using them.

## Iframe compatibility

v2 currently keeps **direct iframe** behavior for remote URLs. Some websites deliberately block framing with CSP `frame-ancestors` or `X-Frame-Options`; this implementation does not pretend those restrictions can be universally bypassed.

A later compatibility mode can be added behind a per-overlay renderer option, but it should preserve the one-Browser-Source model and be tested against real overlay providers before becoming default behavior.

## Testing checklist

1. Pull the latest repo.
2. Run `build-ui.ps1` and confirm it deploys `CRNTLY.StreamerBot.UI.dll` into Streamer.bot's `dlls` directory.
3. Restart Streamer.bot after replacing a DLL that was already loaded.
4. `overlayer-v2.cs` should only need `Newtonsoft.Json.dll` as its project-specific editor reference.
5. Paste the current `overlayer-v2.cs` into the Execute C# Code sub-action and compile it.
6. Run the action and confirm the script-owned WPF window opens.
7. Confirm existing `overlayer/listview.json` entries appear.
8. Test Name/URL/Width/Height/X/Y editing and 500 ms autosave.
9. Test the individual Width/Height/X/Y reset buttons and reset-all.
10. Test duplicate, reorder, delete, enable/disable, copy URL, open source and pop-out compositor actions.
11. Start the server and add one OBS Browser Source at `http://localhost:42069/`.
12. Confirm slider movement previews live without writing every drag frame.
13. Test remote overlays and a local HTML overlay with relative CSS/JS/image assets.
14. Stop the server and verify the loaded OBS Browser Source clears rather than freezing stale output.
15. Start the server again and verify the already-loaded Browser Source reconnects.
16. Hide/reopen the management window and verify the server can remain owned by the script lifecycle.
17. Shut down/recompile Streamer.bot and confirm `Dispose()` releases ports 42069/42070.

## Not yet claimed complete

- automatic download/update from livestreaming.tools
- compatibility proxy for iframe-hostile sites
- drag-and-drop ordering
- provider-specific compatibility testing
- automated Windows/WPF build CI
