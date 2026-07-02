// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using System.Linq;
using SharpEmu.Core.Loader;

namespace SharpEmu.Core.Memory;

/// <summary>One region's entry in a <see cref="MemorySnapshot"/>: its layout plus a content fingerprint.</summary>
public readonly record struct RegionSnapshot(
    ulong BaseAddress,
    ulong Size,
    ProgramHeaderFlags Protection,
    bool IsGuard,
    bool IsShared,
    string? Name,
    ulong ContentHash);

/// <summary>
/// A lightweight snapshot of an address space: region layout and a per-region content hash rather than a
/// full byte copy, so it is cheap to take repeatedly and to <see cref="Diff"/> in order to see which
/// regions appeared, disappeared or changed between two points in time.
/// </summary>
public sealed class MemorySnapshot
{
    public MemorySnapshot(IReadOnlyList<RegionSnapshot> regions)
    {
        Regions = regions;
        TotalMappedBytes = regions.Aggregate(0UL, (sum, region) => sum + region.Size);
    }

    public IReadOnlyList<RegionSnapshot> Regions { get; }

    public ulong TotalMappedBytes { get; }

    /// <summary>Compares this snapshot (taken earlier) with a later one.</summary>
    public MemorySnapshotDiff Diff(MemorySnapshot later)
    {
        var before = Regions.ToDictionary(region => region.BaseAddress);
        var after = later.Regions.ToDictionary(region => region.BaseAddress);

        var added = after.Keys.Where(address => !before.ContainsKey(address)).OrderBy(address => address).ToArray();
        var removed = before.Keys.Where(address => !after.ContainsKey(address)).OrderBy(address => address).ToArray();
        var changed = after.Keys
            .Where(address => before.TryGetValue(address, out var old) &&
                              (old.ContentHash != after[address].ContentHash || old.Protection != after[address].Protection))
            .OrderBy(address => address)
            .ToArray();

        return new MemorySnapshotDiff(added, removed, changed);
    }
}

/// <summary>The set of region base addresses that differ between two <see cref="MemorySnapshot"/> instances.</summary>
public readonly record struct MemorySnapshotDiff(
    IReadOnlyList<ulong> Added,
    IReadOnlyList<ulong> Removed,
    IReadOnlyList<ulong> Changed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}
