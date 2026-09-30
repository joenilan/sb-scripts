# sb-scripts

C# tools and reusable UI infrastructure for [Streamer.bot](https://streamer.bot), developed under the CRNTLY and [livestreaming.tools](https://livestreaming.tools/) brands.

## Current scripts

| Script | Version | Setup guide | Summary |
| --- | --- | --- | --- |
| [Overlay(er)](overlay-er.cs) | 2.2.1 | [Setup and reference](docs/OVERLAY_ER.md) | Manage web and local overlays through one OBS Browser Source. |
| [Mr. Operator](mroperator.cs) | 3.2.0 | [Setup guide](docs/MR_OPERATOR_SETUP.md) | A nine-line call desk with emote, Channel Point reward, or combined caller entry. |

The source file is pasted into a Streamer.bot C# action. Each setup guide explains its references, runtime components, triggers, configuration, use, and troubleshooting.

## Adding or updating a script

Every maintained script needs a dedicated setup guide before it is treated as ready for other people to install. Start from [the setup guide template](docs/SCRIPT_SETUP_TEMPLATE.md) and follow the repository rules in [AGENTS.md](AGENTS.md).

When adding a current script:

1. Add its source at the repository root and give it an explicit version.
2. Add a complete guide under docs, based on the template. Check every instruction against the source and the actual Streamer.bot action shape.
3. Add the script and guide to this README, including current version and editor/runtime dependencies.
4. If it should appear in Zombie.Digital’s script library, check the site’s script catalog and add or update its release, download, and setup links as needed.
5. If an existing script is retired, move it to deprecated and identify its replacement there. Do not list it as an installable current script.

Keep the guide current whenever configuration, triggers, references, runtime dependencies, storage, ports, or the user workflow changes.

## CRNTLY Streamer.bot UI

[CRNTLY.StreamerBot.UI](CRNTLY.StreamerBot.UI/) is the shared WPF runtime and component library for CRNTLY Streamer.bot tools. It has no Streamer.bot dependency and contains no tool-specific window. Each script owns its product layout and behavior; the library provides reusable WPF hosting, the shared visual system, controls, and reflection-friendly script bridges.

The current runtime is version 1.1.0 and targets .NET Framework 4.8.1. Overlay(er) and Mr. Operator load it at runtime from:

    <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll

Download the [latest compiled DLL](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll) or see [all releases](https://github.com/joenilan/sb-scripts/releases). Restart Streamer.bot after replacing a DLL that the process has already loaded.

Build on Windows from the repository root:

    .\build-ui.ps1

The build script attempts to find Streamer.bot and deploys the DLL to its dlls directory. If needed, pass the install location:

    .\build-ui.ps1 -StreamerBotPath 'C:\path\to\streamer.bot'

Streamer.bot’s current external C# editor guidance targets net481 with WPF enabled, which is also the target used by the shared runtime.

## Overlay(er) 2.2.1

The current source is [overlay-er.cs](overlay-er.cs). It owns the Overlay(er) window, editing and autosave behavior, settings, local-file picker, compositor server, and OBS-facing behavior. The shared CRNTLY DLL supplies generic WPF hosting, shared controls and styles, and the script bridge.

The Streamer.bot C# editor needs Newtonsoft.Json.dll as a compile-time reference. CRNTLY.StreamerBot.UI.dll is loaded dynamically at runtime and does not need to be added as an editor reference. The action has one Execute C# Code sub-action and no event trigger; run it manually to open the window.

Use the [Overlay(er) setup and reference](docs/OVERLAY_ER.md) for installation, OBS setup, local files, saved settings, ports, and troubleshooting.

## Mr. Operator 3.2.0

The current source is [mroperator.cs](mroperator.cs). Set MrOperatorBuild.CallEntryMode near the top of the source to PhoneEmote, CallInReward, or Both. Both is the default. The Twitch Chat Message trigger is required in every mode for Hangup and connected-caller speech. Add a Twitch Reward Redemption trigger to the same action when CallInReward or Both is selected. One Execute C# Code sub-action routes both event types; no separate Execute C# Method action is needed.

The C# editor needs System.Speech.dll. The CRNTLY UI DLL is a runtime dependency. See the [Mr. Operator setup guide](docs/MR_OPERATOR_SETUP.md) for the full action setup and workflow.

## Deprecated scripts

The original Overlay(er) v1.0.0 source is retained under [deprecated](deprecated/README.md) for historical reference. New installations should use the current Overlay(er) source and guide above.

## General Streamer.bot workflow

For each current script, follow its dedicated setup guide. In general, create the action and triggers that guide specifies, add the required editor references, paste the complete source into its Execute C# Code sub-action, compile, save, and initialize it as directed. Do not assume every script uses the same triggers or runtime dependencies.
