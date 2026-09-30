# Mr. Operator v3.1.0 setup

Mr. Operator supports either or both caller-entry methods:

- **Phone emote:** a viewer sends the `Phone` emote as the entire chat message.
- **Call In reward:** a viewer redeems a Twitch Channel Point reward named `Call In`.

The reward is optional. Hangup is supported in either configuration: the connected caller can end their call with it, and a waiting caller can use it to leave the queue. Only the connected caller can end the active call.

The emotes used in the original setup are [Phone on 7TV](https://7tv.app/emotes/01F7D5AX20000F6K9D5B7CEHRH) and [Hangup on 7TV](https://7tv.app/emotes/01GZSVBDN8000EN5J9C5Q05KCN). Make sure your viewers can use them in chat through 7TV or your emote setup.

## Install the shared UI component

1. Put `CRNTLY.StreamerBot.UI.dll` in `<Streamer.bot>\dlls\`.
2. Restart Streamer.bot after updating the DLL.
3. In the Streamer.bot C# editor, add the `System.Speech.dll` reference.

The current shared UI DLL is available from the [latest CRNTLY release](https://github.com/joenilan/sb-scripts/releases/latest/download/CRNTLY.StreamerBot.UI.dll).

## Create one Streamer.bot action

1. Create an action named **Mr. Operator**.
2. Add a Twitch **Chat Message** trigger. This is needed for Phone, Hangup, and reading the connected caller's messages aloud.
3. If you want the Channel Point option, add a Twitch **Reward Redemption** trigger to the same action and create a reward titled exactly **Call In**. If you want emotes only, skip this trigger and reward.
4. Add one **Core → C# → Execute C# Code** sub-action to the action and paste the complete [`mroperator.cs`](https://github.com/joenilan/sb-scripts/blob/main/mroperator.cs) source into it.
5. Compile and save the action.
6. Run the action manually once to open the switchboard. The window stays available while the action runtime is active.

There is no additional Execute C# Method action to create. The script routes the reward and chat events through the one C# sub-action.

## Use the call board

1. Click **Open Lines** when you're ready to take calls. If Call In exists, the script enables it; Phone remains available either way.
2. Viewers join through the method or methods you enabled. The script adds callers to the waiting queue and announces their position.
3. Click a waiting caller's line to connect them. Their chat messages are read aloud using the selected Windows voice.
4. The connected caller can send **Hangup**, or you can click their active line to end the call. A waiting caller can send **Hangup** to withdraw.
5. After a call ends, the waiting list advances. The next caller is not connected automatically; click their line when you're ready.

## Local test sequence

Use a second Twitch account while the action is running:

1. Open Lines and send `Phone` by itself. Confirm the account appears in the queue.
2. Send `Phone` again and confirm it does not add a duplicate entry.
3. Click that caller's line, then send a normal chat message. Confirm it is read aloud.
4. Send `Hangup` from the connected account and confirm the line clears.
5. If using rewards, redeem **Call In** from another account and confirm it enters the same queue.
6. Send `Hangup` from a waiting account and confirm it leaves the queue. A different viewer's Hangup must not end the connected caller's call.
