# Mr. Operator 3.2.0 setup guide

Mr. Operator is a nine-line call desk for Streamer.bot. Viewers can join through the Phone emote, a Twitch Channel Point reward, or either method. You choose the supported method in the source before compiling.

Current files:

- Source: [mroperator.cs](../mroperator.cs)
- Shared UI runtime: [CRNTLY.StreamerBot.UI.dll](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll)

## Choose how viewers call

Near the top of mroperator.cs, set MrOperatorBuild.CallEntryMode:

| Value | Caller entry | Required Streamer.bot trigger |
| --- | --- | --- |
| PhoneEmote | Send the Phone emote as the entire chat message. | Twitch Chat Message |
| CallInReward | Redeem the Twitch Channel Point reward named Call In. | Twitch Chat Message and Twitch Reward Redemption |
| Both | Accept either Phone or Call In. This is the default. | Twitch Chat Message and Twitch Reward Redemption |

The Twitch Chat Message trigger is required in every mode. It handles the Phone and Hangup emotes when enabled, and reads messages from the connected caller aloud. The Hangup emote works whether calls are opened by Phone or by reward.

The emotes in the original setup are [Phone on 7TV](https://7tv.app/emotes/01F7D5AX20000F6K9D5B7CEHRH) and [Hangup on 7TV](https://7tv.app/emotes/01GZSVBDN8000EN5J9C5Q05KCN). Make sure the emotes are available to your channel and appear to Streamer.bot as the standalone chat text Phone and Hangup. The parser compares the complete message, ignoring letter case.

For reward modes, create a Twitch Channel Point reward whose title is exactly Call In. The script looks up that reward and enables or disables it when you open or close lines. In Both mode, a missing reward is logged and Phone calls remain available. In CallInReward mode, the reward must exist for setup to succeed.

## Requirements

- Windows and Streamer.bot with its C# editor.
- System.Speech.dll added as a compile-time reference in the Streamer.bot C# editor.
- CRNTLY.StreamerBot.UI.dll in Streamer.bot’s dlls folder at:

      <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
- At least one enabled Windows speech voice for spoken caller messages.
- The Phone and Hangup emotes available to chat if you use emote entry.
- A Call In reward and its redemption trigger if you use reward entry.

Download the current shared UI runtime from the [latest release](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll). Restart Streamer.bot after replacing a DLL already loaded by the process.

The UI DLL is loaded dynamically; do not add it as a compile-time reference. The voice synthesis reference is System.Speech.dll.

## Create the Streamer.bot action

Use one action for initialization, chat messages, and reward redemptions:

1. Set MrOperatorBuild.CallEntryMode in the source to the mode you want.
2. Create an action named Mr. Operator.
3. Add a Twitch Chat Message trigger to that action.
4. If the mode is CallInReward or Both, add a Twitch Reward Redemption trigger to the same action. Skip this trigger in PhoneEmote mode.
5. Add one Core → C# → Execute C# Code sub-action to the action.
6. Add System.Speech.dll to the C# editor references.
7. Paste the complete current mroperator.cs source, compile, and save.
8. Run the action manually once to initialize the runtime and open the call desk.

No separate Execute C# Method action is needed. The script reads the current Streamer.bot event type and routes chat and reward events through the same C# sub-action. Run the action manually before testing triggers; this creates the window and event-handling runtime. Keep the Streamer.bot runtime active while using the call desk.

If the action does not compile, check the System.Speech.dll reference. If the window reports that the CRNTLY component is missing, check the DLL location and restart Streamer.bot.

## Take calls

1. Click Open Lines when you are ready to accept callers. Mr. Operator announces that lines are open and enables the Call In reward when that method is configured.
2. Viewers join by sending Phone by itself, redeeming Call In, or either one, according to the selected mode.
3. Waiting callers appear in the line deck. Select any waiting caller to connect them; the selection is manual, so callers are not automatically put on air.
4. The connected caller’s normal chat messages are spoken using the selected Windows voice.
5. The connected caller can send Hangup, or you can click their active line to end the call.
6. A waiting caller can send Hangup to leave the queue.
7. Click Open Lines again to close entry. Existing active calls can finish; callers cannot start a new call while lines are closed.

Only the connected caller can end their call by sending Hangup. A different viewer’s Hangup does not end the active caller’s call.

## Speech and chat filter controls

- Voice lists enabled Windows voices. Choose a voice installed on the computer running Streamer.bot.
- Output Level changes the speech volume from 0 to 100 percent.
- Mute caller speech suppresses spoken messages without ending the call.
- Replace filtered words enables the word filter. Enter words separated by commas, semicolons, or new lines.

These controls and the current call queue belong to the active script runtime. The script does not save them to a settings file; reinitializing or restarting the runtime resets the queue and control selections.

## Twitch access and local networking

Configure the Chat Message and Reward Redemption triggers for the Twitch channel where the call desk will run. In reward modes, Streamer.bot’s Twitch connection must be able to find and enable or disable that channel’s Call In reward. Mr. Operator does not open a local HTTP port or use an OBS Browser Source.

## Manual verification

Use a second Twitch account while the action and call desk are running:

1. Open Lines. Confirm the on-screen notice and chat message show the selected call method.
2. In PhoneEmote or Both mode, send Phone as a standalone message. Confirm the viewer is queued. Send it again and confirm the viewer is not added twice.
3. In CallInReward or Both mode, redeem Call In and confirm the viewer enters the same queue.
4. Select a waiting caller’s line. Confirm the caller becomes active and the timer starts.
5. Send a normal chat message from the connected account. Confirm it is spoken.
6. Test the selected voice, volume, mute, and word filter controls.
7. Send Hangup from the active caller and confirm the call ends. Connect another caller and test ending the call by clicking the active line.
8. Queue another viewer and send Hangup from that waiting account. Confirm that viewer leaves and the active call remains untouched.
9. Close Lines and confirm new calls are rejected. If a caller is active, confirm that call can finish.
10. If using reward mode, confirm the Call In reward is enabled when lines open and disabled when they close.

## Troubleshooting

### Phone or Hangup does not work

The complete chat message must be Phone or Hangup, ignoring case. Confirm the relevant emote is enabled for your channel and that Streamer.bot’s Chat Message event receives its name as the message text. Check the selected CallEntryMode and keep the Twitch Chat Message trigger on the action in every mode.

### A reward redemption does not join the queue

Confirm the source is set to CallInReward or Both, the reward title is exactly Call In, and the same action has a Twitch Reward Redemption trigger. Run the action manually first so the runtime exists.

### The chat says the switchboard is closed

Click Open Lines. A valid Phone or reward call is accepted only while lines are open.

### Caller messages are not spoken

Confirm the Chat Message trigger is attached, the caller is currently connected, Mute caller speech is off, and Output Level is above zero. Select an enabled voice installed in Windows. If speech initialization fails, check the Streamer.bot log and the System.Speech.dll editor reference.

### The CRNTLY window does not open

Place CRNTLY.StreamerBot.UI.dll in the Streamer.bot dlls directory and restart Streamer.bot after updating it. Confirm the action compiles with System.Speech.dll referenced, then run it manually. The Streamer.bot log contains the detailed startup error.

### Reward mode cannot start

For CallInReward, create the Call In Twitch reward before running the action and confirm Streamer.bot can access it. In Both mode, Phone remains available if the reward is missing, but reward entry cannot work until the reward and trigger are configured.

## Version and source

- Mr. Operator: 3.2.0
- CRNTLY.StreamerBot.UI runtime: 1.1.0
- Source: [mroperator.cs](../mroperator.cs)
- Shared UI DLL: [latest release download](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll)
- Repository releases: [GitHub releases](https://github.com/joenilan/sb-scripts/releases)
