# Repository instructions

This repository contains current Streamer.bot scripts, the shared CRNTLY UI runtime, and their installation documentation. Treat source code, setup guides, and the root README as one product: keep their versions, dependencies, and action instructions aligned.

## Every maintained script needs a setup guide

Before a new user-facing script is considered ready:

1. Put its current source at the repository root.
2. Add a dedicated guide under docs using [docs/SCRIPT_SETUP_TEMPLATE.md](docs/SCRIPT_SETUP_TEMPLATE.md).
3. Verify each step against the script source and the actual Streamer.bot action and trigger structure.
4. Update the current-script table and dependency notes in README.md.
5. If the script is intended for the Zombie.Digital library, follow the automatic catalog rules below. A normal script needs no per-script site code change.

A setup guide must cover purpose and version, requirements, editor references versus runtime DLLs, configuration choices, action and trigger setup, compile and initialization steps, ordinary use, saved state and paths, network or permissions, manual verification, troubleshooting, and known limits. Mark irrelevant sections as not applicable or omit them when the template allows it; do not leave generic placeholders in a published guide.

Update the guide whenever a change affects configuration, triggers, dependencies, persistence, ports, permissions, normal workflow, or troubleshooting. Read the source and relevant UI/DLL documentation before describing behavior. Do not copy setup steps from another script without confirming they apply.

## Keep current and historical scripts distinct

Only maintained scripts belong in the current-script list. When a script is retired, move it under deprecated/, add or update the notice in deprecated/README.md, and identify the supported replacement. Deprecated scripts are historical references: do not present them as installable, update their setup guides as current instructions, or add them to the website’s current catalog.

## Keep repository docs aligned

- README.md is the index for current scripts, their versions, guides, and shared runtime.
- Keep the current-script table columns as source link, version, setup-guide link, and summary. Zombie.Digital reads those links and summaries from the table to publish the full guide link.
- Each maintained script has one dedicated setup guide under docs.
- Shared UI runtime requirements belong in CRNTLY.StreamerBot.UI/README.md and the consuming script guides.
- Search for stale filenames, versions, dependencies, and links after moving or renaming files.

## Automatic Zombie.Digital catalog

The Zombie.Digital scripts library discovers every root-level .cs file in this repository through GitHub. The detail route is dynamic, so a normal new script does not need a manual entry in Zombie.Digital or a site rebuild.

For each script that should appear on the site:

1. Keep its current source at the repository root.
2. Add a row to the Current scripts table in README.md with exactly these columns: Script, Version, Setup guide, Summary.
3. Link the first column directly to the root .cs filename. Link the third column to the guide at docs/<SCRIPT_SETUP>.md. Keep that as a repository-relative docs/ path ending in .md.
4. Put the version and dependency metadata in the source header so the site can read it. Use a ProductName and Version constant where the script has them, then list editor references under “Editor reference required:” and runtime files under “Runtime dependency:”.
5. Push the source, guide, and README row to main. The site reads repository metadata with a five-minute revalidation window, so the entry or new detail page may take a few minutes to appear.

The site takes the description from the final README table column and builds the full-guide link from the third column. It extracts the title and version and reads editor/runtime dependencies from the source header. The raw script download and GitHub source link are generated automatically.

Edit zombie-digital/lib/scripts/data.ts only when the new script needs custom site copy, highlights, setup steps, a pinned source URL, or inclusion in FALLBACK_FILES for periods when GitHub’s contents API is unavailable. Such changes are site code changes and need the normal Zombie.Digital deployment.

## Documentation quality

Write for someone installing the script without help from its author. Give exact Streamer.bot trigger and sub-action names, distinguish manual startup from event triggers, and state when a separate action or Execute C# Method is or is not required. Include exact DLL names, paths, configuration defaults, and storage locations where known. Keep claims about tested behavior and known limits precise.
