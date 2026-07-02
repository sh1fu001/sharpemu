// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using SharpEmu.Core.Loader;

namespace SharpEmu.Core.Memory;

/// <summary>
/// A strict, fully managed guest address space. Unlike the loader-backing <see cref="VirtualMemory"/> store
/// and the OS-backed <see cref="PhysicalVirtualMemory"/>, this layer enforces RX/RW/RWX permissions on every
/// access, turns every illegal access into a structured <see cref="MemoryAccessViolation"/> (never a bare
/// crash), and offers an mmap-like API: page-aligned allocation, explicit mapping, protection changes, guard
/// pages, named shared memory, invalid-access tracking and lightweight snapshots. It is deliberately safe and
/// unit-testable so the memory layer — the pillar the GPU, kernel and runtime all sit on — can be reasoned
/// about in isolation.
/// </summary>
public sealed class GuestAddressSpace
{
    public const ulong PageSize = 0x1000;

    private const int MaxTrackedViolations = 128;

    private readonly object _gate = new();
    private readonly List<Region> _regions = new();
    private readonly Dictionary<string, SharedMemory> _sharedByName = new(StringComparer.Ordinal);
    private readonly Queue<MemoryAccessViolation> _recentViolations = new();
    private readonly ulong _minAddress;
    private readonly ulong _maxAddress;
    private long _violationCount;

    public GuestAddressSpace(ulong minAddress = 0x1_0000, ulong maxAddress = 0x1_0000_0000_0000)
    {
        if (maxAddress <= minAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAddress), "maxAddress must be greater than minAddress.");
        }

        _minAddress = AlignUp(minAddress, PageSize);
        _maxAddress = AlignDown(maxAddress, PageSize);
    }

    public long ViolationCount
    {
        get
        {
            lock (_gate)
            {
                return _violationCount;
            }
        }
    }

    public IReadOnlyList<MemoryAccessViolation> RecentViolations
    {
        get
        {
            lock (_gate)
            {
                return _recentViolations.ToArray();
            }
        }
    }

    // --- mmap-like API ---------------------------------------------------------------------------------

    /// <summary>Allocates a page-aligned region at the first free address that satisfies <paramref name="alignment"/>.</summary>
    public ulong Allocate(ulong size, ProgramHeaderFlags protection, ulong alignment = PageSize, string? name = null)
    {
        var alignedSize = ValidateSize(size);
        var effectiveAlignment = NormalizeAlignment(alignment);
        lock (_gate)
        {
            var address = FindFreeAddress(alignedSize, effectiveAlignment);
            InsertRegion(new Region(address, alignedSize, protection, isGuard: false, NewBacking(alignedSize), backingOffset: 0, shared: null, name));
            return address;
        }
    }

    /// <summary>Maps a region at an explicit, page-aligned address (fixed mapping).</summary>
    public void MapAt(ulong address, ulong size, ProgramHeaderFlags protection, string? name = null)
    {
        var alignedSize = ValidateSize(size);
        RequireAligned(address, nameof(address));
        lock (_gate)
        {
            EnsureFree(address, alignedSize);
            InsertRegion(new Region(address, alignedSize, protection, isGuard: false, NewBacking(alignedSize), backingOffset: 0, shared: null, name));
        }
    }

    /// <summary>Maps a no-access guard region; any access to it faults with <see cref="MemoryViolationReason.GuardPage"/>.</summary>
    public void MapGuard(ulong address, ulong size = PageSize, string? name = null)
    {
        var alignedSize = ValidateSize(size);
        RequireAligned(address, nameof(address));
        lock (_gate)
        {
            EnsureFree(address, alignedSize);
            InsertRegion(new Region(address, alignedSize, ProgramHeaderFlags.None, isGuard: true, backing: null, backingOffset: 0, shared: null, name));
        }
    }

    /// <summary>Creates a named shared-memory object that can be mapped into the space one or more times.</summary>
    public SharedMemory CreateSharedMemory(string name, ulong size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var alignedSize = ValidateSize(size);
        lock (_gate)
        {
            if (_sharedByName.ContainsKey(name))
            {
                throw new InvalidOperationException($"Shared memory '{name}' already exists.");
            }

            var shared = new SharedMemory(name, NewBacking(alignedSize));
            _sharedByName.Add(name, shared);
            return shared;
        }
    }

    public bool TryGetSharedMemory(string name, out SharedMemory shared)
    {
        lock (_gate)
        {
            return _sharedByName.TryGetValue(name, out shared!);
        }
    }

    /// <summary>Maps an existing shared-memory object at an explicit address; writes alias every other mapping.</summary>
    public void MapSharedAt(ulong address, SharedMemory shared, ProgramHeaderFlags protection, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(shared);
        RequireAligned(address, nameof(address));
        var size = shared.Size;
        lock (_gate)
        {
            EnsureFree(address, size);
            InsertRegion(new Region(address, size, protection, isGuard: false, shared.Data, backingOffset: 0, shared, name));
        }
    }

    /// <summary>Changes the protection of a page-aligned range, splitting the owning region if needed (mprotect-like).</summary>
    public void Protect(ulong address, ulong size, ProgramHeaderFlags protection)
    {
        RequireAligned(address, nameof(address));
        var alignedSize = ValidateSize(size);
        lock (_gate)
        {
            var index = FindRegionIndex(address);
            if (index < 0)
            {
                throw new InvalidOperationException($"No region is mapped at 0x{address:X16}.");
            }

            var region = _regions[index];
            var end = address + alignedSize;
            if (region.IsGuard || end > region.End)
            {
                throw new InvalidOperationException(
                    $"Protect range 0x{address:X16}-0x{end:X16} must lie within a single non-guard region.");
            }

            _regions.RemoveAt(index);
            if (address > region.Start)
            {
                _regions.Insert(index++, region.Slice(region.Start, address - region.Start, region.Protection));
            }

            _regions.Insert(index++, region.Slice(address, alignedSize, protection));
            if (end < region.End)
            {
                _regions.Insert(index, region.Slice(end, region.End - end, region.Protection));
            }
        }
    }

    /// <summary>Removes the region that starts exactly at <paramref name="address"/>.</summary>
    public void Unmap(ulong address)
    {
        lock (_gate)
        {
            var index = FindRegionIndex(address);
            if (index < 0 || _regions[index].Start != address)
            {
                throw new InvalidOperationException($"No region starts at 0x{address:X16}.");
            }

            _regions.RemoveAt(index);
        }
    }

    // --- access ----------------------------------------------------------------------------------------

    public bool TryRead(ulong address, Span<byte> destination) => TryRead(address, destination, out _);

    public bool TryWrite(ulong address, ReadOnlySpan<byte> source) => TryWrite(address, source, out _);

    public bool TryRead(ulong address, Span<byte> destination, out MemoryAccessViolation? violation)
    {
        lock (_gate)
        {
            if (!TryResolve(address, (ulong)destination.Length, MemoryAccessKind.Read, out var region, out var offset, out violation))
            {
                return false;
            }

            if (!destination.IsEmpty)
            {
                region!.Backing.AsSpan(region.BackingOffset + (int)offset, destination.Length).CopyTo(destination);
            }

            violation = null;
            return true;
        }
    }

    public bool TryWrite(ulong address, ReadOnlySpan<byte> source, out MemoryAccessViolation? violation)
    {
        lock (_gate)
        {
            if (!TryResolve(address, (ulong)source.Length, MemoryAccessKind.Write, out var region, out var offset, out violation))
            {
                return false;
            }

            if (!source.IsEmpty)
            {
                source.CopyTo(region!.Backing.AsSpan(region.BackingOffset + (int)offset, source.Length));
            }

            violation = null;
            return true;
        }
    }

    /// <summary>Reads or throws <see cref="MemoryAccessException"/> — a clean error instead of a raw crash.</summary>
    public void Read(ulong address, Span<byte> destination)
    {
        if (!TryRead(address, destination, out var violation))
        {
            throw new MemoryAccessException(violation!);
        }
    }

    public void Write(ulong address, ReadOnlySpan<byte> source)
    {
        if (!TryWrite(address, source, out var violation))
        {
            throw new MemoryAccessException(violation!);
        }
    }

    // --- inspection ------------------------------------------------------------------------------------

    public bool TryDescribe(ulong address, out MemoryRegionInfo info)
    {
        lock (_gate)
        {
            var index = FindRegionIndex(address);
            if (index < 0)
            {
                info = default;
                return false;
            }

            info = _regions[index].Describe();
            return true;
        }
    }

    public IReadOnlyList<MemoryRegionInfo> SnapshotRegions()
    {
        lock (_gate)
        {
            var snapshot = new MemoryRegionInfo[_regions.Count];
            for (var i = 0; i < _regions.Count; i++)
            {
                snapshot[i] = _regions[i].Describe();
            }

            return snapshot;
        }
    }

    public MemorySnapshot CaptureSnapshot()
    {
        lock (_gate)
        {
            var regions = new RegionSnapshot[_regions.Count];
            for (var i = 0; i < _regions.Count; i++)
            {
                var region = _regions[i];
                regions[i] = new RegionSnapshot(
                    region.Start,
                    region.Size,
                    region.Protection,
                    region.IsGuard,
                    region.Shared is not null,
                    region.Name,
                    region.ContentHash());
            }

            return new MemorySnapshot(regions);
        }
    }

    // --- internals -------------------------------------------------------------------------------------

    private bool TryResolve(
        ulong address,
        ulong length,
        MemoryAccessKind access,
        out Region? region,
        out ulong offset,
        out MemoryAccessViolation? violation)
    {
        region = null;
        offset = 0;

        var index = FindRegionIndex(address);
        if (index < 0)
        {
            violation = Record(address, length, access, MemoryViolationReason.Unmapped, null);
            return false;
        }

        var candidate = _regions[index];
        if (candidate.IsGuard)
        {
            violation = Record(address, length, access, MemoryViolationReason.GuardPage, candidate.Describe());
            return false;
        }

        offset = address - candidate.Start;
        if (length > candidate.Size - offset)
        {
            violation = Record(address, length, access, MemoryViolationReason.OutOfBounds, candidate.Describe());
            return false;
        }

        var permitted = access == MemoryAccessKind.Write ? candidate.CanWrite : candidate.CanRead;
        if (!permitted)
        {
            violation = Record(address, length, access, MemoryViolationReason.ProtectionDenied, candidate.Describe());
            return false;
        }

        region = candidate;
        violation = null;
        return true;
    }

    private MemoryAccessViolation Record(
        ulong address,
        ulong length,
        MemoryAccessKind access,
        MemoryViolationReason reason,
        MemoryRegionInfo? region)
    {
        var violation = new MemoryAccessViolation(address, access, length, reason, region);
        _violationCount++;
        _recentViolations.Enqueue(violation);
        while (_recentViolations.Count > MaxTrackedViolations)
        {
            _recentViolations.Dequeue();
        }

        return violation;
    }

    // Binary search for the region with the greatest Start <= address that still contains it.
    private int FindRegionIndex(ulong address)
    {
        var lo = 0;
        var hi = _regions.Count - 1;
        var found = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_regions[mid].Start <= address)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return found >= 0 && address < _regions[found].End ? found : -1;
    }

    private ulong FindFreeAddress(ulong size, ulong alignment)
    {
        var cursor = AlignUp(_minAddress, alignment);
        foreach (var region in _regions)
        {
            if (region.End <= cursor)
            {
                continue;
            }

            if (region.Start >= cursor && region.Start - cursor >= size)
            {
                break;
            }

            cursor = AlignUp(region.End, alignment);
        }

        if (cursor >= _maxAddress || _maxAddress - cursor < size)
        {
            throw new InvalidOperationException("The guest address space has no free range large enough for the allocation.");
        }

        return cursor;
    }

    private void EnsureFree(ulong address, ulong size)
    {
        var end = address + size;
        foreach (var region in _regions)
        {
            if (address < region.End && region.Start < end)
            {
                throw new InvalidOperationException($"Address range 0x{address:X16}-0x{end:X16} overlaps an existing region.");
            }
        }
    }

    private void InsertRegion(Region region)
    {
        var lo = 0;
        var hi = _regions.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_regions[mid].Start < region.Start)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        _regions.Insert(lo, region);
    }

    private static byte[] NewBacking(ulong size) => new byte[checked((int)size)];

    private static ulong ValidateSize(ulong size)
    {
        if (size == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        var aligned = AlignUp(size, PageSize);
        if (aligned > int.MaxValue)
        {
            throw new NotSupportedException("Regions larger than 2 GB are not supported by the managed address space.");
        }

        return aligned;
    }

    private static ulong NormalizeAlignment(ulong alignment)
    {
        if (alignment == 0)
        {
            return PageSize;
        }

        if ((alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a power of two.");
        }

        return Math.Max(alignment, PageSize);
    }

    private static void RequireAligned(ulong address, string paramName)
    {
        if ((address & (PageSize - 1)) != 0)
        {
            throw new ArgumentException($"Address 0x{address:X16} must be page-aligned.", paramName);
        }
    }

    private static ulong AlignDown(ulong value, ulong alignment) => value & ~(alignment - 1);

    private static ulong AlignUp(ulong value, ulong alignment) => checked((value + (alignment - 1)) & ~(alignment - 1));

    private sealed class Region
    {
        public Region(
            ulong start,
            ulong size,
            ProgramHeaderFlags protection,
            bool isGuard,
            byte[]? backing,
            int backingOffset,
            SharedMemory? shared,
            string? name)
        {
            Start = start;
            Size = size;
            Protection = protection;
            IsGuard = isGuard;
            Backing = backing ?? Array.Empty<byte>();
            BackingOffset = backingOffset;
            Shared = shared;
            Name = name;
        }

        public ulong Start { get; }

        public ulong Size { get; }

        public ProgramHeaderFlags Protection { get; }

        public bool IsGuard { get; }

        public byte[] Backing { get; }

        public int BackingOffset { get; }

        public SharedMemory? Shared { get; }

        public string? Name { get; }

        public ulong End => Start + Size;

        public bool CanRead => (Protection & ProgramHeaderFlags.Read) != 0;

        public bool CanWrite => (Protection & ProgramHeaderFlags.Write) != 0;

        public MemoryRegionInfo Describe() => new(Start, Size, Protection, IsGuard, Name);

        // Produces a sub-region that continues to reference the same backing store (used to split on Protect).
        public Region Slice(ulong start, ulong size, ProgramHeaderFlags protection) =>
            new(start, size, protection, IsGuard, Backing, BackingOffset + (int)(start - Start), Shared, Name);

        public ulong ContentHash()
        {
            const ulong fnvOffsetBasis = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            var hash = fnvOffsetBasis;
            if (IsGuard || Backing.Length == 0)
            {
                return hash;
            }

            var span = Backing.AsSpan(BackingOffset, checked((int)Size));
            foreach (var value in span)
            {
                hash = (hash ^ value) * fnvPrime;
            }

            return hash;
        }
    }
}
