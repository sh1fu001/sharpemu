// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SharpEmu.Libs.Kernel;

/// <summary>
/// Compile-time-visible "port" that <c>SharpEmu.Core.Memory.PhysicalVirtualMemory</c> implements
/// so <see cref="KernelVirtualRangeAllocator"/> can call it directly instead of through
/// reflection (see MEM-003 / analysis/hle-runtime-007-live-debugger-capture.md). This has to be
/// public and to live here in SharpEmu.Libs rather than on <c>IVirtualMemory</c> itself:
/// <c>SharpEmu.Core</c> already project-references <c>SharpEmu.Libs</c>, so a reference in the
/// other direction (Libs -> Core) would be circular. Defining the port on the lower-level
/// assembly and letting Core implement it is the standard way to break that cycle.
/// </summary>
public interface IReservableVirtualMemory
{
    ulong AllocateAt(ulong desiredAddress, ulong size, bool executable, bool allowAlternative);

    bool ReleaseAt(ulong address, ulong size);

    bool TryAllocateAtOrAbove(ulong desiredAddress, ulong size, bool executable, ulong alignment, out ulong actualAddress);
}

internal static class KernelVirtualRangeAllocator
{
    // ctx.Memory is always a SharpEmu.Core.Cpu.TrackedCpuMemory wrapping the
    // IReservableVirtualMemory instance (in practice always PhysicalVirtualMemory - see
    // SharpEmuRuntime.cs/CpuDispatcher.cs; SharpEmu.Core.Memory.VirtualMemory is never
    // instantiated anywhere in the codebase). TrackedCpuMemory itself is not visible from this
    // assembly (no project reference), so unwrapping it still needs a single reflective property
    // lookup by name ("Inner"). This is deliberately kept minimal and separate from the actual
    // allocation calls: it is a plain property getter with no exception-heavy call graph, and is
    // not implicated by the "Invalid Program: attempted to call a UnmanagedCallersOnly method
    // from managed code" crash documented in
    // analysis/hle-runtime-007-live-debugger-capture.md, which is specifically triggered by
    // MethodBase.Invoke of the allocation methods themselves. That reflective Invoke is what this
    // refactor removes, replacing it with direct calls through IReservableVirtualMemory.
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _innerProperties = new();

    public static bool TryReserve(
        CpuContext ctx,
        ulong desiredAddress,
        ulong length,
        bool executable,
        ulong alignment,
        bool allowSearch,
        bool allowAllocateAtAlternative,
        string traceName,
        out ulong mappedAddress)
    {
        mappedAddress = 0;
        if (length == 0)
        {
            return false;
        }

        try
        {
            if (!TryResolveTarget(ctx.Memory, out var target))
            {
                Console.Error.WriteLine($"[LOADER][TRACE] {traceName}: AllocateAt missing on {ctx.Memory.GetType().FullName}");
                return false;
            }

            if (allowSearch &&
                target.TryAllocateAtOrAbove(desiredAddress, length, executable, alignment, out var searchedAddress) &&
                searchedAddress != 0)
            {
                mappedAddress = searchedAddress;
                return true;
            }

            var allocated = target.AllocateAt(desiredAddress, length, executable, allowAllocateAtAlternative);
            if (allocated == 0)
            {
                Console.Error.WriteLine($"[LOADER][TRACE] {traceName}: AllocateAt returned 0");
                return false;
            }

            mappedAddress = allocated;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LOADER][TRACE] {traceName}: AllocateAt invocation threw: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static bool TryRelease(CpuContext ctx, ulong address, ulong length, string traceName)
    {
        if (address == 0 || length == 0)
        {
            return false;
        }

        try
        {
            if (!TryResolveTarget(ctx.Memory, out var target))
            {
                return false;
            }

            return target.ReleaseAt(address, length);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LOADER][TRACE] {traceName}: ReleaseAt invocation threw: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TryResolveTarget(object rootMemory, out IReservableVirtualMemory target)
    {
        object current = rootMemory;

        for (var depth = 0; depth < 4; depth++)
        {
            if (current is IReservableVirtualMemory direct)
            {
                target = direct;
                return true;
            }

            var innerProperty = _innerProperties.GetOrAdd(current.GetType(), FindInnerProperty);
            if (innerProperty is null)
            {
                break;
            }

            var innerValue = innerProperty.GetValue(current);
            if (innerValue is null || ReferenceEquals(innerValue, current))
            {
                break;
            }

            current = innerValue;
        }

        target = null!;
        return false;
    }

    private static PropertyInfo? FindInnerProperty(Type type) =>
        type.GetProperty("Inner", BindingFlags.Public | BindingFlags.Instance);
}
