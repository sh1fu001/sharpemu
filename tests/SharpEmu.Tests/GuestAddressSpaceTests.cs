// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using Xunit;

namespace SharpEmu.Tests;

public sealed class GuestAddressSpaceTests
{
    private const ProgramHeaderFlags Rw = ProgramHeaderFlags.Read | ProgramHeaderFlags.Write;
    private const ProgramHeaderFlags Rx = ProgramHeaderFlags.Read | ProgramHeaderFlags.Execute;

    [Fact]
    public void Allocate_ThenWriteRead_RoundTripsWithinRwRegion()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rw);

        var payload = new byte[] { 1, 2, 3, 4 };
        Assert.True(space.TryWrite(address, payload));

        var readBack = new byte[4];
        Assert.True(space.TryRead(address, readBack));
        Assert.Equal(payload, readBack);
    }

    [Fact]
    public void Allocate_HonoursRequestedAlignment()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rw, alignment: 0x10000);
        Assert.Equal(0UL, address % 0x10000);
    }

    [Fact]
    public void Write_ToExecutableReadOnlyRegion_IsRejectedWithRegionDescription()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rx);

        Assert.True(space.TryRead(address, new byte[4])); // reads are allowed on RX
        Assert.False(space.TryWrite(address, new byte[4], out var violation));

        Assert.NotNull(violation);
        Assert.Equal(MemoryViolationReason.ProtectionDenied, violation!.Reason);
        Assert.Equal(MemoryAccessKind.Write, violation.Access);
        Assert.Equal("RX executable segment", violation.Region!.Value.DescribeRole());
    }

    [Fact]
    public void Read_Unmapped_ReturnsUnmappedViolation()
    {
        var space = new GuestAddressSpace();

        Assert.False(space.TryRead(0xDEAD_0000, new byte[4], out var violation));
        Assert.Equal(MemoryViolationReason.Unmapped, violation!.Reason);
        Assert.Contains("Region: unmapped", violation.Format());
    }

    [Fact]
    public void GuardPage_FaultsOnAnyAccess()
    {
        var space = new GuestAddressSpace();
        space.MapGuard(0x5_0000, GuestAddressSpace.PageSize, "stack-guard");

        Assert.False(space.TryRead(0x5_0000, new byte[1], out var violation));
        Assert.Equal(MemoryViolationReason.GuardPage, violation!.Reason);
        Assert.Contains("guard page", violation.Format());
    }

    [Fact]
    public void Read_SpanningRegionEnd_ReturnsOutOfBounds()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rw);

        Assert.False(space.TryRead(address + 0xFFC, new byte[8], out var violation));
        Assert.Equal(MemoryViolationReason.OutOfBounds, violation!.Reason);
    }

    [Fact]
    public void SharedMemory_MappedTwice_AliasesTheSameBytes()
    {
        var space = new GuestAddressSpace();
        var shared = space.CreateSharedMemory("gpu-ring", 0x1000);
        space.MapSharedAt(0x1_0000, shared, Rw);
        space.MapSharedAt(0x2_0000, shared, Rw);

        space.Write(0x1_0000, new byte[] { 9, 8, 7, 6 });

        var readBack = new byte[4];
        Assert.True(space.TryRead(0x2_0000, readBack));
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, readBack);
    }

    [Fact]
    public void Protect_SplitsRegion_AndEnforcesNewProtectionOnTheMiddle()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x3000, Rw);

        space.Protect(address + 0x1000, 0x1000, ProgramHeaderFlags.Read);

        Assert.Equal(3, space.SnapshotRegions().Count);
        Assert.True(space.TryWrite(address, new byte[1]));            // first page still RW
        Assert.True(space.TryWrite(address + 0x2000, new byte[1]));   // last page still RW
        Assert.False(space.TryWrite(address + 0x1000, new byte[1], out var violation));
        Assert.Equal(MemoryViolationReason.ProtectionDenied, violation!.Reason);
    }

    [Fact]
    public void Unmap_RemovesRegion()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rw);
        space.Unmap(address);

        Assert.False(space.TryRead(address, new byte[1], out var violation));
        Assert.Equal(MemoryViolationReason.Unmapped, violation!.Reason);
    }

    [Fact]
    public void MapAt_OverlappingExistingRegion_Throws()
    {
        var space = new GuestAddressSpace();
        space.MapAt(0x4_0000, 0x1000, Rw);

        Assert.Throws<InvalidOperationException>(() => space.MapAt(0x4_0000, 0x1000, Rw));
    }

    [Fact]
    public void Snapshot_Diff_ReportsChangedRegions()
    {
        var space = new GuestAddressSpace();
        var address = space.Allocate(0x1000, Rw);
        var before = space.CaptureSnapshot();

        space.Write(address, new byte[] { 0x42 });
        var after = space.CaptureSnapshot();

        var diff = before.Diff(after);
        Assert.False(diff.IsEmpty);
        Assert.Contains(address, diff.Changed);
    }

    [Fact]
    public void Violations_AreTrackedAndCounted()
    {
        var space = new GuestAddressSpace();

        Assert.False(space.TryRead(0x123_000, new byte[4], out _));
        Assert.False(space.TryWrite(0x456_000, new byte[4], out _));

        Assert.Equal(2, space.ViolationCount);
        Assert.Equal(2, space.RecentViolations.Count);
    }

    [Fact]
    public void Read_Checked_ThrowsCleanExceptionInsteadOfCrashing()
    {
        var space = new GuestAddressSpace();

        var exception = Assert.Throws<MemoryAccessException>(() => space.Read(0x99_9000, new byte[1]));
        Assert.Equal(MemoryViolationReason.Unmapped, exception.Violation.Reason);
        Assert.StartsWith("MEMORY_ACCESS_VIOLATION", exception.Message);
    }
}
