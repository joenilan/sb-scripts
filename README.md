# sb-scripts

C# scripts and reusable UI infrastructure for [Streamer.bot](https://streamer.bot), developed under the **CRNTLY** / [livestreaming.tools](https://livestreaming.tools/) family.

## Scripts

| Script | Status | Description |
| --- | --- | --- |
| [`overlayer.cs`](overlayer.cs) | v1 / stable baseline | Original Overlay(er): combines multiple URLs into one OBS Browser Source with a WinForms control panel. |
| [`overlayer-v2.cs`](overlayer-v2.cs) | preview | CRNTLY modernization: WPF UI DLL, live compositor state, cleaner local-file routing, streaming I/O, dynamic local DLL loading, and Streamer.bot lifecycle cleanup. |

## CRNTLY Streamer.bot UI

[`CRNTLY.StreamerBot.UI`](CRNTLY.StreamerBot.UI/) is a reusable WPF library for CRNTLY Streamer.bot tools. It deliberately does not depend on Streamer.bot types; scripts keep ownership of `CPH`, platform/OBS integration, persistence, and runtime behavior.

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

`overlayer-v2.cs` does **not** reference `CRNTLY.StreamerBot.UI.dll` at compile time. It looks for the DLL under Streamer.bot's `dlls` directory and loads a small reflection-friendly bridge at runtime.

That means the action can compile even when the CRNTLY component is missing. In the current local test phase, running the action without the DLL shows a bootstrap dialog explaining where the component was expected and asks the tester to run `build-ui.ps1`.

Later, that same bootstrap point can offer a confirmed download/install from livestreaming.tools without changing the rest of Overlayer.

### overlayer-v2.cs references

The only project-specific reference currently required in the Streamer.bot C# editor is:

- `Newtonsoft.Json.dll`

The bootstrap itself avoids a direct WinForms reference as well; its temporary missing-component dialog is invoked through reflection.

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
6. Run the action.

The repo tracks source and supporting projects; Streamer.bot remains the runtime host.
