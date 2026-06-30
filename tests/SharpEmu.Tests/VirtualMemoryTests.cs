// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using Xunit;

namespace SharpEmu.Tests;

public sealed class VirtualMemoryTests
{
    private const ProgramHeaderFlags Rw = ProgramHeaderFlags.Read | ProgramHeaderFlags.Write;

    [Fact]
    public void Map_ThenRead_ReturnsBackedData()
    {
        var memory = new VirtualMemory();
        var data = new byte[] { 1, 2, 3, 4 };
        memory.Map(0x1000, 0x10, 0, data, Rw);

        var buffer = new byte[4];
        Assert.True(memory.TryRead(0x1000, buffer));
        Assert.Equal(data, buffer);
    }

    [Fact]
    public void Read_PastFileDataButWithinRegion_ReturnsZeroFill()
    {
        var memory = new VirtualMemory();
        memory.Map(0x2000, 0x10, 0, new byte[] { 0xAA }, Rw);

        var buffer = new byte[4];
        Assert.True(memory.TryRead(0x2001, buffer));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, buffer);
    }

    [Fact]
    public void WriteThenRead_RoundTrips()
    {
        var memory = new VirtualMemory();
        memory.Map(0x3000, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw);

        var payload = new byte[] { 9, 8, 7, 6, 5 };
        Assert.True(memory.TryWrite(0x3040, payload));

        var readBack = new byte[payload.Length];
        Assert.True(memory.TryRead(0x3040, readBack));
        Assert.Equal(payload, readBack);
    }

    [Fact]
    public void Read_UnmappedAddress_ReturnsFalse()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);

        Assert.False(memory.TryRead(0x9000, new byte[1]));
    }

    [Fact]
    public void Read_SpanningRegionEnd_ReturnsFalse()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);

        // offset 0x0C + 8 bytes = 0x14 > region length 0x10
        Assert.False(memory.TryRead(0x100C, new byte[8]));
    }

    [Fact]
    public void Read_LastByteOfRegion_Succeeds()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);

        Assert.True(memory.TryRead(0x100F, new byte[1]));
        Assert.False(memory.TryRead(0x1010, new byte[1])); // one past the end
    }

    [Fact]
    public void Map_Overlapping_Throws()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw);

        Assert.Throws<InvalidOperationException>(
            () => memory.Map(0x1080, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw));
    }

    [Fact]
    public void Map_AdjacentRegions_DoNotOverlap()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw);
        memory.Map(0x1100, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw); // starts exactly at previous end

        Assert.Equal(2, memory.SnapshotRegions().Count);
    }

    [Fact]
    public void Map_ZeroSize_Throws()
    {
        var memory = new VirtualMemory();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => memory.Map(0x1000, 0, 0, ReadOnlySpan<byte>.Empty, Rw));
    }

    [Fact]
    public void Map_FileDataLargerThanMemory_Throws()
    {
        var memory = new VirtualMemory();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => memory.Map(0x1000, 4, 0, new byte[8], Rw));
    }

    [Fact]
    public void Clear_RemovesAllRegions()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);
        memory.Clear();

        Assert.Empty(memory.SnapshotRegions());
        Assert.False(memory.TryRead(0x1000, new byte[1]));
    }

    [Fact]
    public void ManyRegions_EachResolvesToItsOwnData()
    {
        var memory = new VirtualMemory();
        const int count = 256;
        for (var i = 0; i < count; i++)
        {
            var address = 0x10000UL + (ulong)i * 0x1000UL;
            memory.Map(address, 0x10, 0, new byte[] { (byte)i }, Rw);
        }

        // Probe every region (out of insertion order) to exercise resolution.
        for (var i = count - 1; i >= 0; i--)
        {
            var address = 0x10000UL + (ulong)i * 0x1000UL;
            var buffer = new byte[1];
            Assert.True(memory.TryRead(address, buffer));
            Assert.Equal((byte)i, buffer[0]);
        }

        Assert.Equal(count, memory.SnapshotRegions().Count);
    }

    [Fact]
    public void Map_OutOfOrder_ResolvesCorrectlyAndKeepsRegionsSorted()
    {
        var memory = new VirtualMemory();
        memory.Map(0x3000, 0x10, 0, new byte[] { 3 }, Rw);
        memory.Map(0x1000, 0x10, 0, new byte[] { 1 }, Rw);
        memory.Map(0x2000, 0x10, 0, new byte[] { 2 }, Rw);

        var buffer = new byte[1];
        Assert.True(memory.TryRead(0x1000, buffer));
        Assert.Equal(1, buffer[0]);
        Assert.True(memory.TryRead(0x2000, buffer));
        Assert.Equal(2, buffer[0]);
        Assert.True(memory.TryRead(0x3000, buffer));
        Assert.Equal(3, buffer[0]);

        var regions = memory.SnapshotRegions();
        Assert.Equal(0x1000UL, regions[0].VirtualAddress);
        Assert.Equal(0x2000UL, regions[1].VirtualAddress);
        Assert.Equal(0x3000UL, regions[2].VirtualAddress);
    }

    [Fact]
    public void Read_InGapOrOutsideAllRegions_ReturnsFalse()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);
        memory.Map(0x3000, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw);

        Assert.False(memory.TryRead(0x2000, new byte[1])); // gap between regions
        Assert.False(memory.TryRead(0x0500, new byte[1])); // before the first region
        Assert.False(memory.TryRead(0x9000, new byte[1])); // after the last region
    }

    [Fact]
    public void Map_StartingInsideLowerNeighbor_Throws()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x100, 0, ReadOnlySpan<byte>.Empty, Rw);

        Assert.Throws<InvalidOperationException>(
            () => memory.Map(0x1050, 0x10, 0, ReadOnlySpan<byte>.Empty, Rw));
    }
}
