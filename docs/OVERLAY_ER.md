# Overlay(er) 2.2.1 setup and reference

Overlay(er) combines web pages and local HTML overlays into one compositor page, so OBS needs only one Browser Source. The current script is [overlay-er.cs](../overlay-er.cs). The old v1.0.0 script is archived under [deprecated](../deprecated/README.md) and is not the current version.

## Quick setup

You need:

- Windows and Streamer.bot with its C# editor.
- Newtonsoft.Json.dll as a compile-time reference in the Streamer.bot editor.
- CRNTLY.StreamerBot.UI.dll in the Streamer.bot dlls directory. The current shared runtime is v1.1.0.
- OBS on the same computer as Streamer.bot.

Install the current [CRNTLY.StreamerBot.UI.dll release](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll) into:

    <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll

Restart Streamer.bot after replacing a DLL that it has already loaded. You can also build and deploy the DLL from this repository on Windows by running .\build-ui.ps1; see the [shared UI project README](../CRNTLY.StreamerBot.UI/README.md).

Create one Streamer.bot action:

1. Add one Core → C# → Execute C# Code sub-action. No event trigger or separate Execute C# Method action is needed.
2. Add Newtonsoft.Json.dll to that C# editor’s references.
3. Paste the complete current overlay-er.cs source into the editor, compile it, and save the action.
4. Run the action manually. It opens the Overlay(er) control window. The script loads CRNTLY.StreamerBot.UI.dll dynamically when it starts.
5. Click Start server in Overlay(er).
6. In OBS, add one Browser Source and set its URL to http://localhost:42069/. OBS and Streamer.bot must run on the same computer because the server listens on localhost.

The CRNTLY DLL is not a compile-time reference for this script. If it is missing or cannot be loaded, the script shows a startup message with the expected path and writes details to the Streamer.bot log.

## Open, hide, and stop

- Running the action opens or reopens the control window.
- The window’s close button hides it; it does not stop the compositor server. Run the action again to show the window.
- Start server makes the compositor available to OBS. Stop server shuts down both local listeners and clears the Browser Source output.
- Auto start saves a preference to start the compositor when the Overlay(er) runtime is first created. It does not start the Streamer.bot action when Streamer.bot launches. The action must still be run.
- If you enable Auto start while the current runtime is already running, the saved preference applies when a new Overlay(er) runtime is created. You can always use Start server for the current session.

The OBS Browser Source can stay loaded while you edit entries. Updates are sent live; it does not need to be removed and recreated for each change.

## Create and arrange overlays

1. Click the plus button to add an overlay, then select it in the list.
2. Enter a name and an absolute source address. Supported schemes are http, https, and file.
3. For a web overlay, paste its web address. Use Open source to check that the address opens in a normal browser.
4. For a local overlay, use the folder button to select its HTML entry file. The selected path is saved as a file URI.
5. Set width and height. Accepted CSS lengths are px, %, vw, and vh; zero is accepted and normalized to 0px.
6. Set left and top offsets. A bare number means pixels; positions also accept px, %, vw, and vh, including negative values.
7. Keep the enable checkbox selected to include the overlay in the compositor. Changes autosave after a short delay; the editor shows Saved when they are committed.
8. Use the list controls to duplicate, move, enable or disable, and delete entries. Enabled items are rendered in list order, with later entries layered above earlier entries.

The reset controls restore the selected size or position field. Reset layout restores its width, height, left, and top values to their defaults. Editing a value that fails validation displays an error and does not commit that edit.

## Local HTML files

When a local HTML file is selected, Overlay(er) serves that file and its relative assets through its local asset listener. Relative CSS, JavaScript, image, font, audio, and video paths resolve from the selected file’s directory. The server only maps the selected overlay’s own folder and files beneath it; it does not combine files from separate overlay folders.

The file picker accepts common HTML, image, and video files, as well as an all-files option. Select an HTML file when you want the page to load related assets from that directory.

## Saved configuration

Overlay(er) reads and writes:

    <Streamer.bot current working directory>\overlayer\listview.json

The path is based on Environment.CurrentDirectory, so it follows the directory Streamer.bot is using as its current working directory. The file stores overlay entries, their order and enabled state, dimensions and positions, source URLs, and the Auto start preference. Existing v1 configuration files are read by the current script.

If you need to preserve or move your setup, close or hide the window after edits show Saved, then copy the overlayer directory from the current working directory. For troubleshooting, check the Streamer.bot log for load and save errors.

## Local endpoints

Both listeners bind to localhost and are intended for local use:

| Address | Purpose |
| --- | --- |
| http://localhost:42069/ | Compositor document used by the OBS Browser Source. |
| http://localhost:42069/state | Current enabled overlay state as JSON. |
| http://localhost:42069/events | Live updates to the compositor. |
| http://localhost:42069/health | Simple health check; returns ok while the main listener is running. |
| http://localhost:42070/local/... | Internal route used to serve selected local files and their relative assets. Do not add this address as the OBS source. |

If another program already owns either port, server startup can fail. The UI will remain open and the error is written to the Streamer.bot log.

## Manual verification

After installation, run this short check:

1. Run the action and confirm the window footer identifies Overlay(er) v2.2.1 and shows the loaded CRNTLY UI runtime version.
2. If you already have entries, confirm they load. Add a temporary web overlay and verify its status changes to Saved.
3. Start the server and open http://localhost:42069/ in a browser. While it is running, http://localhost:42069/health should return ok.
4. Add the same address to one OBS Browser Source. Confirm an enabled overlay appears.
5. Change its size or position and confirm the OBS output updates without refreshing or replacing the source.
6. Stop the server and confirm the loaded Browser Source clears. Start it again and confirm the source reconnects.
7. If you use local files, select a small HTML file that references a relative image or stylesheet and check that both the page and asset load.
8. If using Auto start, enable it, create a fresh script runtime, and confirm the server starts without clicking Start server. The Streamer.bot action itself still needs to be run to create that runtime.
9. Remove the temporary entry when finished.

## Troubleshooting

### The action does not compile

Add Newtonsoft.Json.dll to the Streamer.bot C# editor references. The CRNTLY UI DLL is loaded at runtime and should not be added as a direct reference for this script.

### The window says the CRNTLY UI component is missing

Confirm the file is named CRNTLY.StreamerBot.UI.dll and is in the Streamer.bot dlls directory. Install the current release and restart Streamer.bot, then run the action again. If startup still fails, read the full load error in the Streamer.bot log.

### The server does not start or immediately shows offline

Check the Streamer.bot log for the listener error. Confirm ports 42069 and 42070 are available and that local security software is not blocking Streamer.bot from opening local listeners. While running, open the main /health address above to distinguish a listener problem from an OBS source problem.

### OBS shows a blank page

Confirm the server badge says SERVER ONLINE and the Browser Source address is exactly http://localhost:42069/. Verify the overlay is enabled and has a valid http, https, or file source. For a remote page, try opening its source address separately to confirm the page itself works.

### A local page loads without its images, scripts, or styles

Use the folder button to select the intended HTML entry file. Confirm the asset paths in that HTML are relative to the selected file’s folder and that the referenced files exist beneath that folder.

### A remote site refuses to appear

Some sites prohibit being displayed inside another page using Content Security Policy frame-ancestors or X-Frame-Options. Overlay(er) preserves direct iframe behavior and cannot make every site permit embedding. Use a source that allows framing.

### The new setting or source does not appear in OBS

Wait for the editor status to report Saved and confirm the overlay is enabled. The compositor pushes changes while running. If the browser has disconnected, check the server badge and reload the Browser Source once to reconnect.

## Runtime design

The current design keeps product behavior in the script and reusable UI infrastructure in the shared DLL:

- overlay-er.cs owns the window layout, validation, autosave, local-file selection, configuration, ordering, and compositor behavior.
- CRNTLY.StreamerBot.UI.dll owns reusable WPF window hosting, the shared theme and controls, and the generic script bridge.
- OBS loads one transparent compositor document. That page receives overlay state through server-sent events and polls /state every five seconds as a reconciliation fallback.

This boundary lets other CRNTLY scripts share the same UI infrastructure without importing Overlay(er)-specific code.

## Version and source

- Overlay(er): 2.2.1
- CRNTLY.StreamerBot.UI runtime: 1.1.0
- Current script source: [overlay-er.cs](../overlay-er.cs)
- DLL release: [latest release download](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll)
- Repository releases: [GitHub releases](https://github.com/joenilan/sb-scripts/releases)
