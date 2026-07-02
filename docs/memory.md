<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Guest Memory

Guest memory is the layer shared by the CPU, GPU, and kernel. When it is
fragile, faults surface as raw host crashes that are difficult to trace back to
a NID, syscall, or GPU submit. This document describes the memory layer and the
structured errors it produces instead of a bare crash.

## Implementations

| Type | Backing | Used by | Notes |
|---|---|---|---|
| `VirtualMemory` | One `byte[]` per mapped region | Loader and tests | Simple, safe segment store used while parsing and mapping images. |
| `PhysicalVirtualMemory` | OS pages through `VirtualAlloc` and `VirtualProtect` | Live runtime | Guest addresses are host addresses. The CPU and OS enforce protections, and faults arrive as SEH. This path is Windows-only and performance-critical. |
| `GuestAddressSpace` | One managed `byte[]` per region | Tooling, reference code, and tests | Strict managed model that turns every illegal access into a structured `MemoryAccessViolation`. |

All three implement `IVirtualMemory.TryDescribe`, allowing a fault report to
identify the region that was accessed.

## `GuestAddressSpace`

`GuestAddressSpace` is a strict, unit-testable address space with an mmap-like
API:

- **Strict permissions** — every read requires `Read`, and every write requires
  `Write`. `RX`, `RW`, and `RWX` are enforced on every access.
- **Clean page faults** — `TryRead` and `TryWrite` return `false` with a
  `MemoryAccessViolation`. The checked `Read` and `Write` methods throw
  `MemoryAccessException`. Neither path corrupts memory or crashes the host.
- **Guard pages** — `MapGuard` maps a no-access page that reports `GuardPage`
  on any access.
- **Aligned allocation** — `Allocate(size, protection, alignment)` finds the
  first free, aligned range.
- **Shared memory** — `CreateSharedMemory` and `MapSharedAt` alias one backing
  store at multiple addresses.
- **mmap-like API** — `MapAt`, `Protect`, and `Unmap`; `Protect` can split
  regions like `mprotect`.
- **Invalid-access tracking** — recent violations are retained in the bounded
  `RecentViolations` ring.
- **Lightweight snapshots** — `CaptureSnapshot` records the region layout and a
  per-region content hash. `MemorySnapshot.Diff` reports regions that were
  added, removed, or changed.

## `MEMORY_ACCESS_VIOLATION`

Illegal accesses are described by `MemoryAccessViolation`. Its `Format()`
method renders:

```text
MEMORY_ACCESS_VIOLATION
Address: 0x0000000123456789
Access: Write
Region: RX executable segment
Module: eboot.bin
Instruction pointer: 0x0000000100042210
```

- **Region** is derived from the owning region's permissions: `RX`, `RW`,
  `RWX`, guard page, or unmapped.
- **Module** is resolved from the instruction pointer through the kernel module
  registry.
- During a real run, this block is emitted into the memory-fault diagnostics
  and `crash_context.json`.

See the [compatibility matrix](compatibility.md) and the
[README diagnostics section](../README.md#diagnostics) for the surrounding
diagnostic workflow.
