<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# SharpEmu Compatibility Matrix

SharpEmu is an **experimental** PlayStation 5 emulator in a very early alpha
stage. **No commercial games are currently playable.** This document is a
tracking and diagnostic tool, not a promise of support: it records what has
been tested, how far each title gets during boot, which modules and imports
are missing, and what the next technical actions are.

Because the project changes quickly, every entry is a snapshot tied to a
specific emulator commit. Always cross-check a row against its per-game notes
file before drawing conclusions.

> This page tracks progress only. It does **not** describe how to obtain,
> decrypt, or run any game content.

## Status Legend

| Status       | Meaning |
| ------------ | ------- |
| `Not Tested` | The title has not been tested yet. |
| `Loadable`   | The main executable is recognized and loaded. |
| `Booting`    | Execution starts (native code begins running). |
| `Intro`      | The game reaches an initial screen, splash, or video. |
| `Menu`       | The game reaches an interactive menu. |
| `In Game`    | Gameplay is reachable. |
| `Playable`   | Playable with only minor issues. |
| `Broken`     | Crash, hang, or otherwise unusable behavior. |

Statuses are ordered roughly by progress: a title normally moves
`Not Tested` → `Loadable` → `Booting` → `Intro` → `Menu` → `In Game` →
`Playable`. `Broken` is orthogonal and marks a regression or blocking failure
at whatever milestone it occurred.

## Compatibility Table

The rows below are **placeholders** used to illustrate the format. They do not
claim that any real game works.

| Title ID  | Game           | Version | Status     | Last Milestone | Main Blocker            | Missing Modules / Exports | Notes File                                          |
| --------- | -------------- | ------- | ---------- | -------------- | ----------------------- | ------------------------- | --------------------------------------------------- |
| PPSA00000 | Example Game   | Unknown | Not Tested | None           | Unknown                 | Unknown                   | [`PPSA00000.md`](game-notes/PPSA00000.md)           |
| PPSA00001 | Example Game B | Unknown | Loadable   | SELF parsed    | Missing kernel imports  | `libkernel` (partial)     | _not created yet_                                   |
| PPSA00002 | Example Game C | Unknown | Booting    | Entry point    | Unresolved import trap  | Unknown                   | _not created yet_                                   |
| PPSA00003 | Example Game D | Unknown | Broken     | Boot           | Crash during init       | Unknown                   | _not created yet_                                   |

To add a real entry, create its notes file first (see below), then link it in
the last column. Keep the placeholder rows until real data replaces them, or
remove them once the table has real entries.

## Adding a New Tested Game

1. Confirm you are using a **legally obtained** copy of the game (see below).
2. Copy [`game-notes/TEMPLATE.md`](game-notes/TEMPLATE.md) to
   `docs/game-notes/<TITLE_ID>.md` (uppercase Title ID, e.g. `PPSA01341.md`).
3. Run the emulator and capture the logs, for example:
   ```bash
   ./SharpEmu --trace-imports=64 --log-level=debug "path/to/eboot.bin" 2>&1 | tee log.txt
   ```
   Import traces, milestone logs, and execution diagnostics are printed by the
   CLI ([src/SharpEmu.CLI/Program.cs](../src/SharpEmu.CLI/Program.cs)) through
   the `SharpEmu*` logging framework; frame/videoout dumps land in the `logs/`
   directory when the relevant `SHARPEMU_*` environment variables are set.
4. Fill in the notes file: basic info, boot progress, loaded modules, missing
   imports, blockers, and the next technical actions.
5. Add or update the matching row in the table above. Use the **exact** status
   and severity values defined in this document.
6. Keep the row and the notes file in sync whenever you re-test.

## Diagnostics Sessions

Every run writes a structured diagnostics session to
`logs/<TITLE_ID>/<timestamp>/`. These files are the primary source of truth when
filling in a game-notes file:

| File                    | Use it to fill in |
| ----------------------- | ----------------- |
| `boot.log`              | Observed behavior, log paths |
| `imports_missing.json`  | Missing Imports / Exports (NIDs, library, hit counts) |
| `syscalls.json`         | Kernel / HLE blockers (raw syscalls hit) |
| `modules.json`          | Loaded Modules table |
| `memory_map.json`       | Memory-related blockers (mapped regions and protections) |
| `crash_context.json`    | Current Status, Main Blocker, crash RIP / fault address / NID |
| `gpu_submits.json`      | Graphics / AGC blockers (recent GPU submits) |

The session is written on a normal exit and on a guest trap/fault; it is skipped
only if the host process itself is killed abruptly. Pass `--no-diagnostics` (or
set `SHARPEMU_NO_DIAGNOSTICS=1`) to turn it off.

## Interpreting Blockers

A **blocker** is whatever prevents a title from reaching the next milestone.
When reading a row:

- **Main Blocker** is the single most important reason the title is stuck at
  its current status. It should be actionable (e.g. "unresolved `libSceGnmDriver`
  import" rather than "does not work").
- **Missing Modules / Exports** lists the system modules or specific
  exports/NIDs that are not yet implemented and are needed to progress.
- The per-game notes file breaks blockers down by area (kernel/HLE vs.
  graphics/AGC) and assigns each one a **severity** (see next section) so the
  most impactful work can be prioritized.

Two titles at the same status can have very different blockers; always read the
notes file for context before assuming shared causes.

## Blocker Severity Levels

Severity describes the *impact* of a blocker, independent of how far the game
booted. Use these exact values:

| Severity   | Meaning |
| ---------- | ------- |
| `BLOCKER`  | Prevents boot or completely halts execution. |
| `CRITICAL` | Causes a crash, deadlock, or major incorrect behavior. |
| `VISIBLE`  | Has a visible impact on display, audio, or input. |
| `COSMETIC` | Minor impact only. |
| `UNKNOWN`  | Behavior is not yet understood. |

## Naming Conventions

- Per-game notes files: `docs/game-notes/<TITLE_ID>.md`.
  - Example: `docs/game-notes/PPSA01341.md`.
- Title IDs must be written in **UPPERCASE** (e.g. `PPSA01341`, not
  `ppsa01341`).
- Statuses must use exactly the values from the [Status Legend](#status-legend).
- Severities must use exactly the values from the
  [Blocker Severity Levels](#blocker-severity-levels).

## Legal Notice

Only game dumps that were **legally obtained** from hardware you personally own
may be used with SharpEmu. This project does **not** support or condone piracy,
DRM circumvention, or the distribution of copyrighted system firmware or game
data. Do not add entries based on illegally obtained content.
