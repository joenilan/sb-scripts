# Mr. Operator v3.2.0 setup

Choose the caller-entry method by setting `MrOperatorBuild.CallEntryMode` near the top of `mroperator.cs` before compiling:

- `MrOperatorCallEntryMode.PhoneEmote` — use the `Phone` emote by itself in chat.
- `MrOperatorCallEntryMode.CallInReward` — use the Twitch Channel Point reward named `Call In`.
- `MrOperatorCallEntryMode.Both` — accept either one (default).

The active caller can send `Hangup` to end their call, and a waiting caller can use it to leave the queue. Only the connected caller can end the active call.

The emotes used in the original setup are [Phone on 7TV](https://7tv.app/emotes/01F7D5AX20000F6K9D5B7CEHRH) and [Hangup on 7TV](https://7tv.app/emotes/01GZSVBDN8000EN5J9C5Q05KCN). Make sure your viewers can use them in chat through 7TV or your emote setup.

## Install the shared UI component

1. Put `CRNTLY.StreamerBot.UI.dll` in `<Streamer.bot>\dlls\`.
2. Restart Streamer.bot after updating the DLL.
3. In the Streamer.bot C# editor, add the `System.Speech.dll` reference.

The current shared UI DLL is available from the [latest CRNTLY release](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll).

## Create one Streamer.bot action

1. Create an action named **Mr. Operator**.
2. Add a Twitch **Chat Message** trigger. This is required in every mode for Hangup and reading the connected caller's messages aloud; it also handles Phone when that mode is selected.
3. For `CallInReward` or `Both`, add a Twitch **Reward Redemption** trigger to the same action and create a reward titled exactly **Call In**. For `PhoneEmote`, skip the reward trigger and reward.
4. Add one **Core → C# → Execute C# Code** sub-action to the action and paste the complete [`mroperator.cs`](https://github.com/joenilan/sb-scripts/blob/main/mroperator.cs) source into it.
5. Compile and save the action.
6. Run the action manually once to open the switchboard. The window stays available while the action runtime is active.

There is no additional Execute C# Method action to create. The script routes the reward and chat events through the one C# sub-action.

## Use the call board

1. Click **Open Lines** when you're ready to take calls. The selected entry methods appear in the switchboard.
2. Viewers join using the selected method. The script adds callers to the waiting queue and announces their position.
3. Click a waiting caller's line to connect them. Their chat messages are read aloud using the selected Windows voice.
4. The connected caller can send **Hangup**, or you can click their active line to end the call. A waiting caller can send **Hangup** to withdraw.
5. After a call ends, the waiting list advances. The next caller is not connected automatically; click their line when you're ready.

## Local test sequence

Use a second Twitch account while the action is running:

1. Open Lines and use the selected entry method: send `Phone` by itself, redeem `Call In`, or test both. Confirm the account appears in the queue.
2. If using Phone, send it again and confirm it does not add a duplicate entry.
3. Click that caller's line, then send a normal chat message. Confirm it is read aloud.
4. Send `Hangup` from the connected account and confirm the line clears.
5. If using `CallInReward` or `Both`, redeem **Call In** from another account and confirm it enters the same queue.
6. Send `Hangup` from a waiting account and confirm it leaves the queue. A different viewer's Hangup must not end the connected caller's call.
