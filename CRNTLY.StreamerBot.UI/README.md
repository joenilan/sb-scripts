# CRNTLY.StreamerBot.UI

Reusable WPF UI runtime and component library for CRNTLY tools launched from Streamer.bot C# actions.

The DLL intentionally has **no Streamer.bot dependency and no tool-specific windows**. Streamer.bot scripts own their application layout, `CPH`, OBS/platform integration, persistence, validation and behavior. This project owns only reusable WPF infrastructure: STA lifecycle, theme resources, control styles, generic window hosting and reflection-friendly script bridges.

## Target

- .NET Framework 4.8.1 (`net481`)
- WPF
- Assembly: `CRNTLY.StreamerBot.UI.dll`
- Runtime version: `1.0.0`
- Namespace: `Crntly.StreamerBot.UI`

## Intended install model

The end-user experience should stay simple:

```text
Streamer.bot\dlls\CRNTLY.StreamerBot.UI.dll
Execute C# Code: <tool-script>.cs
```

A tool script discovers the DLL dynamically at runtime, so installing a CRNTLY tool does **not** require users to add CRNTLY or WPF assemblies as compile-time references in every Streamer.bot action.

For the current **Overlay(er) v2.0.0** script, `Newtonsoft.Json.dll` remains the only direct editor reference.

## Build

From the repository root on Windows:

```powershell
.\build-ui.ps1
```

That builds and deploys:

```text
CRNTLY.StreamerBot.UI\bin\Release\net481\CRNTLY.StreamerBot.UI.dll
    -> <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
```

## Shared foundation

- `CrntlyUiHost` — dedicated STA/WPF dispatcher so Streamer.bot actions do not block on `ShowDialog()`.
- `ScriptHost/CrntlyScriptWindowBridge` — generic reflection-friendly host for **script-owned XAML**. It loads a Window, exposes named-control properties/methods, resolves theme resources, refreshes item collections and forwards WPF events without requiring the script to reference WPF types at compile time.
- `Theme/` — palette, density tokens, reusable buttons, icon buttons, text inputs, toggles, sliders, scrollbars, tooltips, list rows and other shared CRNTLY visual primitives.

## Ownership rule

A useful test is:

> Would another unrelated CRNTLY Streamer.bot tool reasonably reuse this code unchanged?

If yes, it belongs in `CRNTLY.StreamerBot.UI.dll`.

```text
DLL owns:
  theme tokens
  control templates
  icon / icon-button chrome
  scrollbars
  tooltips
  reusable dialogs/utilities
  window/dispatcher hosting
  generic reflection bridge

Tool script owns:
  its XAML layout
  field names
  button placement
  validation rules
  persistence
  server/OBS behavior
  app-specific callbacks
```

## Overlay(er)

`overlay-er.cs` is a direct example of this boundary:

```text
overlay-er.cs
  -> owns the complete Overlay(er) XAML layout
  -> owns editing/autosave/reset/toolbar behavior
  -> owns config + compositor server + OBS-facing behavior
  -> dynamically loads the generic CrntlyScriptWindowBridge

CRNTLY.StreamerBot.UI.dll
  -> contains no Overlay(er)-specific model, window, bridge or code-behind
```

This keeps **Overlay(er)** portable as one Streamer.bot script plus the common CRNTLY UI runtime, while every future CRNTLY script can reuse the same visual/component system.

## Design boundary

Do not add `CPHInline`, `CPH`, OBS calls, Twitch calls, tool-specific models, or tool-specific layouts to this project. Keep the DLL generic and host-facing; keep application behavior in the script that the user actually installs.
