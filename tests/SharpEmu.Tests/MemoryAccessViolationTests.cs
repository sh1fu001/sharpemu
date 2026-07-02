// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using Xunit;

namespace SharpEmu.Tests;

public sealed class MemoryAccessViolationTests
{
    [Fact]
    public void Format_MatchesTheExpectedDiagnosticBlock()
    {
        var violation = new MemoryAccessViolation(
            0x0000000123456789,
            MemoryAccessKind.Write,
            4,
            MemoryViolationReason.ProtectionDenied,
            new MemoryRegionInfo(0x0000000100000000, 0x1000, ProgramHeaderFlags.Read | ProgramHeaderFlags.Execute),
            "eboot.bin",
            0x0000000100042210);

        var expected = string.Join('\n',
            "MEMORY_ACCESS_VIOLATION",
            "Address: 0x0000000123456789",
            "Access: Write",
            "Region: RX executable segment",
            "Module: eboot.bin",
            "Instruction pointer: 0x0000000100042210");

        Assert.Equal(expected, violation.Format());
    }

    [Fact]
    public void Format_FallsBackWhenRegionModuleAndIpAreUnknown()
    {
        var violation = new MemoryAccessViolation(0xDEAD, MemoryAccessKind.Read, 1, MemoryViolationReason.Unmapped);

        var text = violation.Format();
        Assert.Contains("Region: unmapped", text);
        Assert.Contains("Module: unknown", text);
        Assert.Contains("Instruction pointer: unknown", text);
    }

    [Fact]
    public void VirtualMemory_TryDescribe_ReportsRegionProtection()
    {
        var memory = new VirtualMemory();
        memory.Map(0x1000, 0x100, 0, ReadOnlySpan<byte>.Empty, ProgramHeaderFlags.Read | ProgramHeaderFlags.Execute);

        Assert.True(memory.TryDescribe(0x1050, out var info));
        Assert.Equal(0x1000UL, info.BaseAddress);
        Assert.Equal("RX executable segment", info.DescribeRole());

        Assert.False(memory.TryDescribe(0x9999, out _));
    }
}
