// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;

namespace SharpEmu.Core.Memory;

/// <summary>
/// A read-only description of the region that owns a given address: its bounds, permissions and role.
/// Used both by the guest address space and by <see cref="IVirtualMemory.TryDescribe"/> so a fault can be
/// reported against the region it hit rather than as a bare address.
/// </summary>
public readonly record struct MemoryRegionInfo(
    ulong BaseAddress,
    ulong Size,
    ProgramHeaderFlags Protection,
    bool IsGuard = false,
    string? Name = null)
{
    public ulong EndAddress => BaseAddress + Size;

    public bool CanRead => (Protection & ProgramHeaderFlags.Read) != 0;

    public bool CanWrite => (Protection & ProgramHeaderFlags.Write) != 0;

    public bool CanExecute => (Protection & ProgramHeaderFlags.Execute) != 0;

    /// <summary>Human-readable role such as <c>"RX executable segment"</c> or <c>"RW data segment"</c>.</summary>
    public string DescribeRole()
    {
        if (IsGuard)
        {
            return "guard page";
        }

        if (Protection == ProgramHeaderFlags.None)
        {
            return "no-access region";
        }

        var permissions =
            (CanRead ? "R" : string.Empty) +
            (CanWrite ? "W" : string.Empty) +
            (CanExecute ? "X" : string.Empty);
        var role = CanExecute ? "executable segment" : CanWrite ? "data segment" : "read-only segment";
        var named = string.IsNullOrEmpty(Name) ? string.Empty : $" ({Name})";
        return $"{permissions} {role}{named}";
    }
}
