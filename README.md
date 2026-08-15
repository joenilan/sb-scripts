# sb-scripts

C# scripts for [Streamer.bot](https://streamer.bot).

## Scripts

| Script | Description |
| --- | --- |
| [`overlayer.cs`](overlayer.cs) | Takes multiple URLs and condenses them into a single page for use as an OBS overlay. |

## Usage

Each `.cs` file is a Streamer.bot sub-action script. To use one:

1. In Streamer.bot, create an action and add a **Core > C# > Execute C# Code** sub-action.
2. Paste the contents of the script into the code editor.
3. Add any required references and arguments noted at the top of the script.
4. Compile.

## Notes

Scripts target the C# version and .NET runtime bundled with Streamer.bot, so they
are not built by a project file here — this repo is for tracking and versioning
the source.
