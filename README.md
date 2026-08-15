# sb-scripts

C# scripts and reusable UI infrastructure for [Streamer.bot](https://streamer.bot), developed under the **CRNTLY** / [livestreaming.tools](https://livestreaming.tools/) family.

## Scripts

| Script | Status | Description |
| --- | --- | --- |
| [`overlayer.cs`](overlayer.cs) | v1 / stable baseline | Original Overlay(er): combines multiple URLs into one OBS Browser Source with a WinForms control panel. |
| [`overlayer-v2.cs`](overlayer-v2.cs) | preview | CRNTLY modernization: WPF UI DLL, live compositor state, cleaner local-file routing, streaming I/O, and Streamer.bot lifecycle cleanup. |

## CRNTLY Streamer.bot UI

[`CRNTLY.StreamerBot.UI`](CRNTLY.StreamerBot.UI/) is a reusable WPF library for CRNTLY Streamer.bot tools. It deliberately does not depend on Streamer.bot types; scripts keep ownership of `CPH`, platform/OBS integration, persistence, and runtime behavior.

Build on Windows:

```powershell
.\build-ui.ps1
```

Then add the resulting `CRNTLY.StreamerBot.UI.dll` under **References** in the Streamer.bot C# editor for scripts that use it.

Streamer.bot's current external-editor guidance targets `net481` with WPF enabled, which is also the target used by this DLL.

## overlayer-v2.cs references

Add these under **References** in the C# editor:

- `CRNTLY.StreamerBot.UI.dll`
- `Newtonsoft.Json.dll`

See [`docs/OVERLAYER_V2.md`](docs/OVERLAYER_V2.md) for architecture and the first test checklist.

## overlayer.cs references

The original WinForms script still uses:

- `System.Windows.Forms.dll`
- `System.Drawing.dll`
- `System.Web.dll`
- `Newtonsoft.Json.dll`

## Usage

Each root `.cs` script is intended to remain usable as a Streamer.bot **Core > C# > Execute C# Code** sub-action:

1. Create an action in Streamer.bot.
2. Add an **Execute C# Code** sub-action.
3. Add the references required by the chosen script.
4. Paste the script into the editor.
5. Compile / Save and Compile.

The repo tracks source and supporting projects; Streamer.bot remains the runtime host.
