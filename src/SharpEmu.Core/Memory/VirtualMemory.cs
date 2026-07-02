// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;

namespace SharpEmu.Core.Memory;

public sealed class VirtualMemory : IVirtualMemory
{
    private readonly object _gate = new();
    private readonly List<MappedRegion> _regions = new();

    public void Clear()
    {
        lock (_gate)
        {
            _regions.Clear();
        }
    }

    public void Map(ulong virtualAddress, ulong memorySize, ulong fileOffset, ReadOnlySpan<byte> fileData, ProgramHeaderFlags protection)
    {
        if (memorySize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memorySize), "Memory size must be greater than zero.");
        }

        if ((ulong)fileData.Length > memorySize)
        {
            throw new ArgumentOutOfRangeException(nameof(fileData), "File size cannot exceed memory size.");
        }

        if (memorySize > int.MaxValue)
        {
            throw new NotSupportedException("Virtual memory regions larger than 2 GB are not currently supported.");
        }

        var endAddress = checked(virtualAddress + memorySize);
        var backingMemory = new byte[(int)memorySize];
        fileData.CopyTo(backingMemory);

        lock (_gate)
        {
            // _regions is kept sorted by VirtualAddress so reads/writes can binary
            // search. Regions never overlap, so an overlap can only occur with the
            // immediate neighbours of the insertion point.
            var index = LowerBound(virtualAddress);
            if (index > 0 && _regions[index - 1].EndAddress > virtualAddress)
            {
                throw new InvalidOperationException("Attempted to map an overlapping virtual memory region.");
            }

            if (index < _regions.Count && endAddress > _regions[index].Region.VirtualAddress)
            {
                throw new InvalidOperationException("Attempted to map an overlapping virtual memory region.");
            }

            _regions.Insert(index, new MappedRegion(
                new VirtualMemoryRegion(virtualAddress, memorySize, fileOffset, (ulong)fileData.Length, protection),
                endAddress,
                backingMemory));
        }
    }

    public IReadOnlyList<VirtualMemoryRegion> SnapshotRegions()
    {
        lock (_gate)
        {
            var snapshot = new VirtualMemoryRegion[_regions.Count];
            for (var i = 0; i < _regions.Count; i++)
            {
                snapshot[i] = _regions[i].Region;
            }

            return snapshot;
        }
    }

    public bool TryDescribe(ulong virtualAddress, out MemoryRegionInfo info)
    {
        lock (_gate)
        {
            var lo = 0;
            var hi = _regions.Count - 1;
            var found = -1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (_regions[mid].Region.VirtualAddress <= virtualAddress)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (found >= 0 && virtualAddress < _regions[found].EndAddress)
            {
                var region = _regions[found].Region;
                info = new MemoryRegionInfo(region.VirtualAddress, region.MemorySize, region.Protection);
                return true;
            }

            info = default;
            return false;
        }
    }

    public bool TryRead(ulong virtualAddress, Span<byte> destination)
    {
        lock (_gate)
        {
            if (!TryResolveRegion(virtualAddress, destination.Length, out var region, out var offset))
            {
                return false;
            }

            region.BackingMemory.AsSpan(offset, destination.Length).CopyTo(destination);
            return true;
        }
    }

    public bool TryWrite(ulong virtualAddress, ReadOnlySpan<byte> source)
    {
        lock (_gate)
        {
            if (!TryResolveRegion(virtualAddress, source.Length, out var region, out var offset))
            {
                return false;
            }

            source.CopyTo(region.BackingMemory.AsSpan(offset, source.Length));
            return true;
        }
    }

    private bool TryResolveRegion(ulong virtualAddress, int length, out MappedRegion region, out int offset)
    {
        // Binary search for the last region whose start is <= virtualAddress. Since
        // regions are sorted and non-overlapping, that is the only region that can
        // contain the address.
        var lo = 0;
        var hi = _regions.Count - 1;
        var found = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_regions[mid].Region.VirtualAddress <= virtualAddress)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (found >= 0)
        {
            var candidate = _regions[found];
            if (virtualAddress < candidate.EndAddress)
            {
                var candidateOffset = checked((int)(virtualAddress - candidate.Region.VirtualAddress));
                if (candidateOffset + length <= candidate.BackingMemory.Length)
                {
                    region = candidate;
                    offset = candidateOffset;
                    return true;
                }
            }
        }

        region = default;
        offset = 0;
        return false;
    }

    // First index whose region starts at or after the given address (std::lower_bound).
    private int LowerBound(ulong address)
    {
        var lo = 0;
        var hi = _regions.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_regions[mid].Region.VirtualAddress < address)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private readonly record struct MappedRegion(VirtualMemoryRegion Region, ulong EndAddress, byte[] BackingMemory);
}
