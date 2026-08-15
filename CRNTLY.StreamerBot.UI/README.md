# CRNTLY.StreamerBot.UI

Reusable WPF UI components for CRNTLY tools that run from Streamer.bot C# actions.

The DLL intentionally has **no Streamer.bot dependency**. Streamer.bot scripts own `CPH`, OBS/platform integration, persistence, and tool-specific runtime behavior; this project owns WPF lifecycle, CRNTLY styling, and reusable windows/controls.

## Target

- .NET Framework 4.8.1 (`net481`)
- WPF
- Assembly: `CRNTLY.StreamerBot.UI.dll`
- Namespace: `Crntly.StreamerBot.UI`

Streamer.bot's current external-editor recipe targets `net481` with WPF enabled, so the library targets the same runtime boundary.

## Build

From this directory on Windows:

```powershell
dotnet build -c Release
```

Output:

```text
bin\Release\net481\CRNTLY.StreamerBot.UI.dll
```

Add that DLL as a reference in the Streamer.bot **Execute C# Code** editor for scripts that use it.

## Included foundation

- `CrntlyUiHost` — dedicated STA/WPF dispatcher so Streamer.bot actions do not block on `ShowDialog()`.
- `Theme/CrntlyTheme.xaml` — centralized CRNTLY dark palette and base WPF styles.
- `Overlayer/OverlayerWindow` — first consumer: modern Overlayer management UI.
- `Overlayer/OverlayerUi` — thread-safe facade consumed by the Streamer.bot script.
- `Overlayer/OverlayItem` — UI-facing model with no Streamer.bot types.

## Design boundary

Do not add `CPHInline`, `CPH`, OBS calls, Twitch calls, or Streamer.bot plugin assemblies to this project unless there is a compelling reason to change the architecture. A UI library that only knows plain .NET models/callbacks can be reused by every CRNTLY Streamer.bot utility.

Overlayer-specific UI is currently included in this first DLL deliberately. If CRNTLY eventually grows enough tools to justify a separate `CRNTLY.StreamerBot.Core` or tool-specific assemblies, split it then rather than fragmenting the foundation now.
