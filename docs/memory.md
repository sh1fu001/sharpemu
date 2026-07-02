<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Guest Memory

Guest memory is the layer the CPU, GPU and kernel all sit on. When it is fragile, faults surface as raw
host crashes that are very hard to trace back to a NID, syscall or GPU submit. This document describes the
memory layer and the structured error it produces instead of a bare crash.

## Implementations

| Type | Backing | Used by | Notes |
| ---- | ------- | ------- | ----- |
| `VirtualMemory` | one `byte[]` per mapped region | loader / tests | Simple, safe segment store used while parsing and mapping images. |
| `PhysicalVirtualMemory` | OS pages (`VirtualAlloc`/`VirtualProtect`) | the live runtime | Guest addresses are host addresses; the CPU/OS enforce protections and faults arrive as SEH. Windows-only, performance-critical. |
| `GuestAddressSpace` | one `byte[]` per region (managed) | tooling / reference / tests | Strict, fully managed model that turns every illegal access into a structured `MemoryAccessViolation`. |

All three implement `IVirtualMemory.TryDescribe`, so a fault can be reported against the region it hit.

## `GuestAddressSpace`

A strict, unit-testable address space with an mmap-like API:

- **Strict permissions** — every read requires `Read`, every write requires `Write`; `RX`/`RW`/`RWX` are
  enforced on every access.
- **Clean page faults** — `TryRead`/`TryWrite` return `false` with a `MemoryAccessViolation`; the checked
  `Read`/`Write` throw `MemoryAccessException`. Neither ever corrupts memory or crashes the host.
- **Guard pages** — `MapGuard` maps a no-access page that faults with `GuardPage` on any touch.
- **Aligned allocation** — `Allocate(size, protection, alignment)` finds the first free, aligned range.
- **Shared memory** — `CreateSharedMemory` + `MapSharedAt` alias one backing store at several addresses.
- **mmap-like API** — `MapAt`, `Protect` (splits regions like `mprotect`), `Unmap`.
- **Invalid-access tracking** — recent violations are kept in a bounded ring (`RecentViolations`).
- **Lightweight snapshots** — `CaptureSnapshot` records region layout plus a per-region content hash;
  `MemorySnapshot.Diff` reports which regions were added, removed or changed.

## `MEMORY_ACCESS_VIOLATION`

Illegal accesses are described by `MemoryAccessViolation`, whose `Format()` renders:

```
MEMORY_ACCESS_VIOLATION
Address: 0x0000000123456789
Access: Write
Region: RX executable segment
Module: eboot.bin
Instruction pointer: 0x0000000100042210
```

- **Region** is derived from the owning region's permissions (`RX`/`RW`/`RWX`, `guard page`, or `unmapped`).
- **Module** is resolved from the instruction pointer via the kernel module registry.
- On a real run this block is emitted into the memory-fault diagnostics and `crash_context.json`
  (see [compatibility.md](compatibility.md) and the Diagnostics section of the README).
