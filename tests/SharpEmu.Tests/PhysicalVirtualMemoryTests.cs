// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Runtime.InteropServices;
using SharpEmu.Core.Memory;
using Xunit;

namespace SharpEmu.Tests;

// PhysicalVirtualMemory uses Win32 VirtualAlloc, so these tests only run on Windows.
public sealed class PhysicalVirtualMemoryTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void AllocateThenReadWrite_RoundTrips()
    {
        if (!IsWindows)
        {
            return;
        }

        using var memory = new PhysicalVirtualMemory();
        var address = memory.AllocateAt(0, 0x2000, executable: false, allowAlternative: true);
        Assert.NotEqual(0UL, address);

        Assert.True(memory.IsAccessible(address, 0x2000));

        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.True(memory.TryWrite(address + 0x40, payload));

        var readBack = new byte[payload.Length];
        Assert.True(memory.TryRead(address + 0x40, readBack));
        Assert.Equal(payload, readBack);
    }

    [Fact]
    public void MultipleAllocations_EachResolvesToItsOwnRegion()
    {
        if (!IsWindows)
        {
            return;
        }

        using var memory = new PhysicalVirtualMemory();

        // Allocate several regions; the OS returns them in arbitrary address order,
        // exercising the sorted insert + binary-search lookup.
        var addresses = new ulong[8];
        for (var i = 0; i < addresses.Length; i++)
        {
            addresses[i] = memory.AllocateAt(0, 0x1000, executable: false, allowAlternative: true);
            Assert.NotEqual(0UL, addresses[i]);
            Assert.True(memory.TryWrite(addresses[i], new[] { (byte)(0x10 + i) }));
        }

        for (var i = addresses.Length - 1; i >= 0; i--)
        {
            var buffer = new byte[1];
            Assert.True(memory.TryRead(addresses[i], buffer));
            Assert.Equal((byte)(0x10 + i), buffer[0]);
        }
    }

    [Fact]
    public void Read_OutsideAnyRegion_ReturnsFalse()
    {
        if (!IsWindows)
        {
            return;
        }

        using var memory = new PhysicalVirtualMemory();
        var address = memory.AllocateAt(0, 0x1000, executable: false, allowAlternative: true);
        Assert.NotEqual(0UL, address);

        // An address well below the allocation cannot resolve to any region.
        Assert.False(memory.IsAccessible(0x1000, 1));
        Assert.False(memory.TryRead(0x1000, new byte[1]));
    }
}
