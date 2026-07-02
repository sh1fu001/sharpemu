<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# SharpEmu Compatibility Matrix

SharpEmu is an experimental PlayStation 5 emulator. This compatibility matrix
is used to track boot progress, blockers, missing exports, and technical notes
for legally obtained test inputs.

Compatibility entries are evidence-based snapshots. A title mentioned in an
issue or the README is not assumed to work unless a reproducible milestone is
documented.

## Status Legend

| Status | Meaning |
|---|---|
| `Not Tested` | The title has not been tested yet. |
| `Loadable` | The main executable is recognized and loaded. |
| `Booting` | Execution starts but does not reach a visible interactive state. |
| `Intro` | The title reaches an initial splash screen, intro, logo, or video. |
| `Menu` | The title reaches an interactive menu. |
| `In Game` | Gameplay is accessible but may have major issues. |
| `Playable` | Gameplay is mostly usable with minor or moderate issues. |
| `Broken` | The title crashes, hangs, or is unusable. |

## Blocker Severity

| Severity | Meaning |
|---|---|
| `BLOCKER` | Prevents boot or fully blocks execution. |
| `CRITICAL` | Causes a crash, deadlock, or major incorrect behavior. |
| `VISIBLE` | Affects graphics, audio, input, or other visible behavior. |
| `COSMETIC` | Minor issue with little impact. |
| `UNKNOWN` | Behavior is not understood yet. |

## Compatibility Table

| Title ID | Game | Version | Status | Last Milestone | Main Blocker | Missing Modules / Exports | Notes File |
|---|---|---|---|---|---|---|---|
| PPSA01341 | Demon's Souls | Unknown | Intro | Video loop and first VideoOut frame reported in README | General shader translation to SPIR-V/Vulkan | Unknown | [`PPSA01341.md`](game-notes/PPSA01341.md) |
| PPSA20591 | Poppy Playtime Chapter 1 | Unknown | Not Tested | No reproducible milestone documented | Unknown; fresh diagnostics required | Unknown | [`PPSA20591.md`](game-notes/PPSA20591.md) |
| PPSA10112 | SILENT HILL: The Short Message | Unknown | Not Tested | No reproducible milestone documented | Unknown; fresh diagnostics required | Unknown | [`PPSA10112.md`](game-notes/PPSA10112.md) |
| PPSA02929 | Dreaming Sarah | Unknown | Intro | Splash texture rendered in README capture | Unknown; fresh diagnostics required | Unknown | [`PPSA02929.md`](game-notes/PPSA02929.md) |

The two `Not Tested` rows mean that no current structured evidence is available
for assigning a boot milestone. They preserve the historical README references
without promoting an unverified compatibility claim.

## Evidence Sources

Use the following sources when updating a row:

- Structured diagnostics under `logs/<TITLE_ID>/<timestamp>/`
- A per-game note tied to a specific emulator commit and test date
- A reproducible screenshot or capture
- An existing README statement, clearly identified as historical evidence

The structured diagnostics files have the following roles:

| File | Purpose |
|---|---|
| `boot.log` | Captures observed boot behavior. |
| `imports_missing.json` | Lists unresolved guest imports and hit counts. |
| `syscalls.json` | Lists raw guest syscalls observed during execution. |
| `modules.json` | Records loaded modules and address ranges. |
| `memory_map.json` | Records mapped guest regions and protections. |
| `gpu_submits.json` | Records recent AGC command-buffer submissions. |
| `shaders.json` | Records identified shader programs and fingerprints. |
| `crash_context.json` | Records the final result, fault, trap, or crash context. |

## Adding or Updating a Title

1. Copy [`game-notes/TEMPLATE.md`](game-notes/TEMPLATE.md) to
   `docs/game-notes/<TITLE_ID>.md`.
2. Run the title using a legally obtained test input.
3. Attach or reference the structured diagnostics produced by the run.
4. Record only milestones supported by those diagnostics or captures.
5. Update the compatibility row and its notes file together.
6. Run `scripts/validate-docs.ps1`.

Example diagnostic run:

```powershell
.\SharpEmu --trace-imports=64 --log-level=debug "path\to\eboot.bin" `
  2>&1 | Tee-Object -FilePath "log.txt"
```

## Naming Conventions

- Game note files must use: `docs/game-notes/<TITLE_ID>.md`
- Example: `docs/game-notes/PPSA01341.md`
- Title IDs must be uppercase.
- Status values must exactly match the values listed in the status legend.
- Severity values must exactly match the values listed in the blocker severity table.

## Legal Notice

Only legally obtained test inputs should be used. This project does not
provide, request, or distribute copyrighted game content, console firmware,
keys, or decrypted proprietary assets.
