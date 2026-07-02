// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using System.Buffers.Binary;
using Xunit;

namespace SharpEmu.Tests;

public sealed class KernelVirtualMemoryTests
{
    private const ulong ScratchAddress = 0x10000;

    [Fact]
    public void ReservedVirtualRange_IsVisibleToVirtualQuery()
    {
        const ulong reservedAddress = 0x0000000600000000;
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        memory.Map(
            reservedAddress,
            0x100000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        var context = new CpuContext(memory, Generation.Gen5);
        KernelMemoryCompatExports.TrackReservedVirtualRange(reservedAddress, 0x100000);

        context[CpuRegister.Rdi] = reservedAddress;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.Rdx] = ScratchAddress + 0x100;
        context[CpuRegister.Rcx] = 72;
        Assert.Equal(0, KernelMemoryCompatExports.KernelVirtualQuery(context));

        Span<byte> info = stackalloc byte[72];
        Assert.True(memory.TryRead(ScratchAddress + 0x100, info));
        Assert.Equal(reservedAddress, BinaryPrimitives.ReadUInt64LittleEndian(info));
        Assert.Equal(
            reservedAddress + 0x100000,
            BinaryPrimitives.ReadUInt64LittleEndian(info[8..]));
    }
}
