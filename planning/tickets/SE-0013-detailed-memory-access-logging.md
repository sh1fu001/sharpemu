<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# SE-0013 — Opt-in detailed virtual-memory access diagnostics

```yaml
id: SE-0013
title: Add opt-in bounded detailed virtual-memory access diagnostics
epic: EPIC-CORE-001
team: memory
team_lead: memory-team-lead
cycle_type: implementation
priority: high
status: READY
objective: >
  Add bounded, opt-in diagnostic logging for virtual-memory lifecycle operations
  and TryRead/TryWrite outcomes, without changing memory behaviour, public
  contracts, or exposing guest-memory contents.
non_objectives:
  - CPU, loader, runtime, GPU, GUI, CLI, HLE, or logging-framework changes
  - changes to the IVirtualMemory public contract or memory semantics
  - logging guest-memory bytes, host pointers, paths, keys, firmware, or game data
  - diagnostics enabled by default or unbounded event emission
minimal_context:
  - AGENTS.md
  - planning/tickets/SE-0013-detailed-memory-access-logging.md
  - agents/context-packs/core-memory.md
  - src/SharpEmu.Core/Memory/IVirtualMemory.cs
  - src/SharpEmu.Core/Memory/VirtualMemory.cs
  - src/SharpEmu.Core/Memory/PhysicalVirtualMemory.cs
  - src/SharpEmu.Logging/SharpEmuLog.cs
  - docs/governance/CLEAN_ROOM_POLICY.md
allowed_files:
  - src/SharpEmu.Core/Memory/VirtualMemory.cs
  - src/SharpEmu.Core/Memory/PhysicalVirtualMemory.cs
  - src/SharpEmu.Core/Memory/MemoryAccessDiagnostics.cs
  - src/SharpEmu.Tests/Memory/**
  - src/SharpEmu.Tests/SharpEmu.Tests.csproj
  - SharpEmu.slnx
  - Directory.Packages.props
  - planning/tickets/SE-0013-detailed-memory-access-logging.md
  - docs/handoffs/SE-0013/**
forbidden_files:
  - src/SharpEmu.Core/Memory/IVirtualMemory.cs
  - src/SharpEmu.Core/Cpu/**
  - src/SharpEmu.Core/Loader/**
  - src/SharpEmu.Core/Runtime/**
  - src/SharpEmu.HLE/**
  - src/SharpEmu.Libs/**
  - src/SharpEmu.Logging/**
  - src/SharpEmu.GUI/**
  - src/SharpEmu.CLI/**
  - .github/**
  - docs/governance/**
  - orchestration/**
dependencies: []
specifications: []
applicable_adrs: []
acceptance_criteria:
  - Detailed diagnostics are disabled unless SHARPEMU_LOG_MEMORY_ACCESSES is exactly 1.
  - Enabled diagnostics emit VMEM Debug entries for successful and failed TryRead and TryWrite operations, including operation, guest address, requested byte count, outcome, and a non-secret failure reason when applicable.
  - Enabled diagnostics emit VMEM Debug entries for applicable map, allocation, free, clear, and protection lifecycle operations.
  - Diagnostics never include byte payloads, host pointers, file paths, game content, keys, firmware, or proprietary data.
  - SHARPEMU_LOG_MEMORY_MAX_EVENTS caps emission; absent, invalid, or zero values use a documented finite default, and cap exhaustion produces at most one VMEM Warning.
  - Disabled diagnostics construct no diagnostic message and allocate no logging payload on TryRead/TryWrite fast paths.
  - Existing success and failure behaviour of memory operations is unchanged.
  - All added files carry the project SPDX header.
required_tests:
  - Disabled, enabled-success, enabled-failure, event-cap, and no-payload diagnostics for VirtualMemory.
  - Windows-guarded PhysicalVirtualMemory coverage for disabled, enabled-success, enabled-failure, and lifecycle diagnostics.
validation_commands:
  - dotnet build SharpEmu.slnx
  - dotnet test
  - python scripts/agents/validate_governance.py
risks:
  - Enabled per-access diagnostics can reduce emulation throughput; an opt-in finite cap bounds event volume.
  - PhysicalVirtualMemory is Windows-specific; tests must use an explicit platform guard.
  - The test-project bootstrap touches solution and package metadata under the user-approved exception.
legal_constraints:
  - Clean-room only; tests use synthetic addresses and data.
  - Never log guest-memory payloads or proprietary material.
deliverables:
  - Opt-in bounded virtual-memory diagnostics implementation
  - Focused automated tests
  - docs/handoffs/SE-0013/SE-0013-H1.md
stop_conditions:
  - A change to IVirtualMemory, SharpEmu.Logging, or any forbidden path is required.
  - Meeting the diagnostics requirement requires memory-payload or unbounded sensitive-data logging.
reviewer: memory-reviewer
validator: core-qa-lead
context_budget: MEDIUM
context_level: L2
context_pack: agents/context-packs/core-memory.md
handoff_format: docs/templates/HANDOFF_TEMPLATE.md
```

## Notes

This ticket supersedes the retired contribution drafts `OBS-001` and `TEST-001`
for this initiative only. The Project Owner approved an exception to consolidate
the diagnostic implementation and focused test bootstrap in this ticket.

## Operational note

`VMEM` access entries use the existing `Debug` level. They are visible when
`SHARPEMU_LOG_LEVEL=Debug` (or a more verbose level) is configured, or in the
existing `SHARPEMU_LOG_FILE` output, which captures all levels. This ticket does
not alter logging-framework filtering behaviour.

PhysicalVirtualMemory diagnostics omit addresses because its returned virtual
addresses can be host-backed. VirtualMemory diagnostics retain their logical
guest addresses.
