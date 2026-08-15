# CRNTLY.StreamerBot.UI

Reusable WPF UI runtime and component library for CRNTLY tools launched from Streamer.bot C# actions.

The DLL intentionally has **no Streamer.bot dependency**. Streamer.bot scripts own their application-specific layout, `CPH`, OBS/platform integration, persistence, and tool behavior. This project owns the difficult reusable WPF pieces: STA lifecycle, theme resources, control styles, window hosting, and reflection-friendly script bridges.

## Target

- .NET Framework 4.8.1 (`net481`)
- WPF
- Assembly: `CRNTLY.StreamerBot.UI.dll`
- Namespace: `Crntly.StreamerBot.UI`

## Intended install model

The end-user experience should stay simple:

```text
Streamer.bot\dlls\CRNTLY.StreamerBot.UI.dll
Execute C# Code: <tool-script>.cs
```

A tool script should be able to discover the DLL dynamically at runtime, so installing a CRNTLY tool does **not** require users to add CRNTLY or WPF assemblies as compile-time references in every Streamer.bot action.

For the current Overlayer script, `Newtonsoft.Json.dll` remains the only direct editor reference.

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
- `ScriptHost/CrntlyScriptWindowBridge` — generic reflection-friendly host for **script-owned XAML**. It loads a Window, exposes named-control properties/methods, and forwards WPF events without requiring the script to reference WPF types at compile time.
- `Theme/` — palette, density tokens, reusable buttons, icon buttons, text inputs, toggles, sliders, scrollbars, tooltips, list rows, and other shared CRNTLY visual primitives.

## Ownership rule

A useful test is:

> Would another unrelated CRNTLY Streamer.bot tool reasonably reuse this code unchanged?

If yes, it belongs in `CRNTLY.StreamerBot.UI.dll`.

Examples:

```text
DLL owns:
  theme tokens
  control templates
  icons / icon-button chrome
  scrollbars
  tooltips
  dialogs
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

This keeps the shared DLL generic while allowing each Streamer.bot tool to remain portable as one script plus the common CRNTLY UI runtime.

## Overlayer migration

`Overlayer/` is still present temporarily as the proven runtime path while the script-owned-XAML host is introduced. Do not add new Overlayer-specific architecture to the shared DLL.

The migration target is:

```text
overlayer-v2.cs
  -> owns Overlayer layout + behavior
  -> loads CRNTLY.StreamerBot.UI.dll dynamically
  -> uses CrntlyScriptWindowBridge for WPF hosting

CRNTLY.StreamerBot.UI.dll
  -> contains no Overlayer-specific window/layout/model classes
```

Once the script-owned path has been tested in Streamer.bot, the DLL's legacy `Overlayer/` folder can be removed.

## Design boundary

Do not add `CPHInline`, `CPH`, OBS calls, Twitch calls, or Streamer.bot plugin assemblies to this project. Keep the DLL generic and host-facing; keep application behavior in the script that the user actually installs.
