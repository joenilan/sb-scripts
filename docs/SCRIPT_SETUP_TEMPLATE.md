# [Script name] [version] setup guide

Briefly say what the script does and who needs this guide. Link the current source file and any release downloads.

## Quick setup

List the prerequisites a user needs before installing. Then give the shortest complete install path:

1. Identify the Streamer.bot action and required triggers.
2. Add editor references and runtime components, with exact names and locations.
3. Paste the complete current source into the correct C# sub-action, compile, and save.
4. Describe how to initialize or run the script and any connected app setup.

State clearly whether the action needs event triggers, a manual first run, or an additional action. If it uses one C# block to handle multiple event types, document how they are routed.

## Requirements

| Requirement | Type | How to install or configure |
| --- | --- | --- |
| [Assembly or component] | Editor reference / runtime DLL / external app / account setting | [Exact name, path, version, or setup link] |

Keep compile-time editor references separate from files that the script loads dynamically at runtime.

## Configure before compiling

Document source constants, enum values, mode choices, reward names, commands, IDs, or other settings that a user must select. State the default and exactly where to change it.

If no pre-build configuration is required, say so.

## Streamer.bot action setup

Describe the complete action structure. Use this table when triggers map to different behavior:

| Trigger or run method | Required configuration | What the script does |
| --- | --- | --- |
| [Trigger] | [Event, reward, or filter configuration] | [Behavior] |

List every required trigger, optional trigger, sub-action, and manual initialization step. Explain which action receives each trigger. State explicitly when users do not need a separate Execute C# Method action.

## Install, compile, and start

Write numbered instructions a user can follow from a clean Streamer.bot setup. Include the current source link and the exact reference names. Explain what success looks like on first run and what to do if a required component is missing.

## Use the script

Explain the normal workflow in the order a user performs it. Describe controls, accepted chat messages or events, who can perform sensitive actions, and how the script handles queues, shutdown, or repeated events when applicable.

## Settings and saved data

Document every setting the user can change, its default, whether it is saved, and where it is stored. If the script does not persist settings or runtime state, say so. Include exact paths relative to the working directory where relevant.

## Network and permissions

List local ports, URLs, firewall needs, API permissions, account roles, reward permissions, or other access requirements. If none apply, say so. Clarify whether an address is local-only and whether another application must run on the same computer.

## Manual verification

Provide a short user-run checklist for the core workflow, important alternate modes, normal shutdown, and the main failure cases. Keep the checklist grounded in behavior available in this version.

## Troubleshooting

Use symptoms as headings. Give likely checks tied to actual error messages, dependencies, settings, triggers, files, or network behavior. Do not claim a generic solution works without verifying it.

## Known limits

State current limitations that affect installation or use, such as site embedding restrictions, platform event requirements, unsupported formats, or restart requirements. Omit this section if there are no known user-facing limits.

## Version and source

- Script: [version]
- Required shared runtime: [version or none]
- Current source: [repository link]
- Current downloads: [release link]
- Full release history: [releases link]
