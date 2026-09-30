# sb-scripts

C# scripts and reusable UI infrastructure for [Streamer.bot](https://streamer.bot), developed under the **CRNTLY** / [livestreaming.tools](https://livestreaming.tools/) family.

## Scripts

| Script | Status | Description |
| --- | --- | --- |
| [`overlayer.cs`](overlayer.cs) | v1.0.0 / legacy baseline | Original **Overlay(er)**: combines multiple URLs into one OBS Browser Source with a WinForms control panel. |
| [`overlay-er.cs`](overlay-er.cs) | **v2.2.1 / current** | Current **Overlay(er)** release: script-owned WPF layout/behavior, shared CRNTLY UI runtime, live compositor state, persisted server auto-start, local-file browser, cleaner local-file routing, streaming I/O, dynamic DLL loading, autosave, live position preview, and Streamer.bot lifecycle cleanup. |
| [`mroperator.cs`](mroperator.cs) | **v3.0.0 / redesigned** | Standalone call desk with a compact caller deck, one-action Twitch event routing, speech controls, and optional word filtering. |

## CRNTLY Streamer.bot UI

[`CRNTLY.StreamerBot.UI`](CRNTLY.StreamerBot.UI/) is the reusable WPF runtime/component library for CRNTLY Streamer.bot tools. It deliberately does not depend on Streamer.bot types and contains no **Overlay(er)**-specific window. Scripts keep ownership of their layout, `CPH`, platform/OBS integration, persistence and runtime behavior.

The shared UI runtime is versioned independently from individual tools. The current runtime is **v1.1.0**; Overlay(er) v2.2.1 displays both its own version and the loaded UI assembly version in the window footer so stale script/DLL combinations are easy to spot. The shared runtime also contains the exact native WPF `ERROR_NOT_ENOUGH_QUOTA` / `HwndTarget` dispatcher failure so a transient CRNTLY window/render-target error cannot become a fatal Streamer.bot thread exception.

Download the current compiled runtime from the latest GitHub release:

- [CRNTLY.StreamerBot.UI.dll](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll)
- [Release notes and previous versions](https://github.com/joenilan/sb-scripts/releases)

Place the DLL in `<Streamer.bot>\dlls\`, restart Streamer.bot, then run any CRNTLY script that lists it as a runtime dependency.

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

## Overlay(er) v2.2.1 bootstrap

`overlay-er.cs` does **not** reference `CRNTLY.StreamerBot.UI.dll` at compile time. The script owns its **Overlay(er)** XAML and UI behavior, then dynamically loads the generic `CrntlyScriptWindowBridge` from the shared DLL for WPF hosting/theme/component support.

This means the action can compile even when the CRNTLY component is missing. In the current local test phase, running the action without the DLL shows a bootstrap dialog explaining where the component was expected and asks the tester to run `build-ui.ps1`.

Later, that same bootstrap point can offer a confirmed download/install from livestreaming.tools without changing the rest of Overlay(er).

The **Auto start** toggle controls only the compositor server. When enabled, the server starts automatically the next time the Overlay(er) runtime starts. It does not auto-run the Streamer.bot action itself.

The **Browse local file** button beside the URL field opens a native Windows file picker and stores the selected file as a `file:///...` URI. v2.2.1 prefers the already-available WinForms common dialog in Streamer.bot and falls back to the WPF dialog when available. The existing isolated local server then serves that entry file and its relative assets through the compositor.

### overlay-er.cs references

The only project-specific reference currently required in the Streamer.bot C# editor is:

- `Newtonsoft.Json.dll`

The bootstrap, clipboard, confirmation and file-picker helpers avoid adding project-specific UI references to the Streamer.bot editor.

See [`docs/OVERLAY_ER.md`](docs/OVERLAY_ER.md) for architecture and the current test checklist.

## Mr. Operator v3.0.0

`mroperator.cs` replaces the legacy WinForms panel with a script-owned WPF call desk and dynamically loads `CrntlyScriptWindowBridge` from `CRNTLY.StreamerBot.UI.dll`. Mr. Operator owns its product layout, caller-line states, and behavior; CRNTLY supplies the shared palette, reusable control templates, icons, scrollbars, tooltips, WPF host, and script bridge. The voice picker uses CRNTLY's shared ComboBox style, added in runtime v1.1.0. The nine-line deck gives each caller one direct action tile, separates waiting and live states, and keeps the connected caller and call timer in a single on-air panel. Its footer shows the script and shared UI versions.

The Twitch Channel Point reward must be named **Call In**. Put both the Twitch **Reward Redemption** and **Chat Message** triggers on one Streamer.bot Action, then add one **Execute C# Code** sub-action containing the full script. `Execute()` routes each event using its event type: redemptions join the queue, and chat from the connected caller is read aloud. Run the action manually once to open the call desk; no separate Execute C# Method actions are needed. Clicking a waiting caller connects them, and clicking the active line ends the call. Waiting callers then move forward in queue order; the next caller is not connected automatically.

### mroperator.cs references

- `System.Speech.dll` (voice synthesis)
- Runtime dependency: `<Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll`

The CRNTLY DLL and WPF framework are loaded at runtime; they are not compile-time references in the Streamer.bot C# editor.

## overlayer.cs references

The original v1.0.0 WinForms script still uses:

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
