# sb-scripts

C# scripts and reusable UI infrastructure for [Streamer.bot](https://streamer.bot), developed under the **CRNTLY** / [livestreaming.tools](https://livestreaming.tools/) family.

## Scripts

| Script | Status | Description |
| --- | --- | --- |
| [`overlayer.cs`](overlayer.cs) | v1 / stable baseline | Original Overlay(er): combines multiple URLs into one OBS Browser Source with a WinForms control panel. |
| [`overlayer-v2.cs`](overlayer-v2.cs) | preview | CRNTLY modernization: script-owned WPF layout/behavior, shared CRNTLY UI runtime, live compositor state, cleaner local-file routing, streaming I/O, dynamic DLL loading, and Streamer.bot lifecycle cleanup. |

## CRNTLY Streamer.bot UI

[`CRNTLY.StreamerBot.UI`](CRNTLY.StreamerBot.UI/) is the reusable WPF runtime/component library for CRNTLY Streamer.bot tools. It deliberately does not depend on Streamer.bot types and contains no Overlayer-specific window. Scripts keep ownership of their layout, `CPH`, platform/OBS integration, persistence and runtime behavior.

Build on Windows:

```powershell
.\build-ui.ps1
```

The build script attempts to find Streamer.bot and deploys the finished DLL to:

```text
<Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
```

You can provide the install location explicitly when needed:

```powershell
.\build-ui.ps1 -StreamerBotPath 'C:\path\to\streamer.bot'
```

Streamer.bot's current external-editor guidance targets `net481` with WPF enabled, which is also the target used by this DLL.

## overlayer-v2.cs bootstrap

`overlayer-v2.cs` does **not** reference `CRNTLY.StreamerBot.UI.dll` at compile time. The script itself owns its Overlayer XAML and UI behavior, then dynamically loads the generic `CrntlyScriptWindowBridge` from the shared DLL for WPF hosting/theme/component support.

This means the action can compile even when the CRNTLY component is missing. In the current local test phase, running the action without the DLL shows a bootstrap dialog explaining where the component was expected and asks the tester to run `build-ui.ps1`.

Later, that same bootstrap point can offer a confirmed download/install from livestreaming.tools without changing the rest of Overlayer.

### overlayer-v2.cs references

The only project-specific reference currently required in the Streamer.bot C# editor is:

- `Newtonsoft.Json.dll`

The bootstrap, clipboard and confirmation helpers avoid direct WinForms references by using reflection.

See [`docs/OVERLAYER_V2.md`](docs/OVERLAYER_V2.md) for architecture and the current test checklist.

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
6. Run the action.

For CRNTLY WPF tools, the intended user-facing install model is simply **one shared DLL in `dlls` + one tool script**.
