# Repository instructions

This repository contains current Streamer.bot scripts, the shared CRNTLY UI runtime, and their installation documentation. Treat source code, setup guides, and the root README as one product: keep their versions, dependencies, and action instructions aligned.

## Every maintained script needs a setup guide

Before a new user-facing script is considered ready:

1. Put its current source at the repository root.
2. Add a dedicated guide under docs using [docs/SCRIPT_SETUP_TEMPLATE.md](docs/SCRIPT_SETUP_TEMPLATE.md).
3. Verify each step against the script source and the actual Streamer.bot action and trigger structure.
4. Update the current-script table and dependency notes in README.md.
5. If the script is intended for the Zombie.Digital library, check that repository’s script catalog and ensure its source, setup, and release links are available.

A setup guide must cover purpose and version, requirements, editor references versus runtime DLLs, configuration choices, action and trigger setup, compile and initialization steps, ordinary use, saved state and paths, network or permissions, manual verification, troubleshooting, and known limits. Mark irrelevant sections as not applicable or omit them when the template allows it; do not leave generic placeholders in a published guide.

Update the guide whenever a change affects configuration, triggers, dependencies, persistence, ports, permissions, normal workflow, or troubleshooting. Read the source and relevant UI/DLL documentation before describing behavior. Do not copy setup steps from another script without confirming they apply.

## Keep current and historical scripts distinct

Only maintained scripts belong in the current-script list. When a script is retired, move it under deprecated/, add or update the notice in deprecated/README.md, and identify the supported replacement. Deprecated scripts are historical references: do not present them as installable, update their setup guides as current instructions, or add them to the website’s current catalog.

## Keep repository docs aligned

- README.md is the index for current scripts, their versions, guides, and shared runtime.
- Keep the current-script table columns as source link, version, setup-guide link, and summary. Zombie.Digital reads those links and summaries from the table to publish the full guide link.
- Each maintained script has one dedicated setup guide under docs.
- Shared UI runtime requirements belong in CRNTLY.StreamerBot.UI/README.md and the consuming script guides.
- If website catalog behavior or metadata changes, make the corresponding update in the Zombie.Digital repository as well.
- Search for stale filenames, versions, dependencies, and links after moving or renaming files.

## Documentation quality

Write for someone installing the script without help from its author. Give exact Streamer.bot trigger and sub-action names, distinguish manual startup from event triggers, and state when a separate action or Execute C# Method is or is not required. Include exact DLL names, paths, configuration defaults, and storage locations where known. Keep claims about tested behavior and known limits precise.
