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
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(info[24..]));
        Assert.Equal(0, info[32] & 0x10);

        var nameBytes = "unity-reserve\0"u8;
        Assert.True(memory.TryWrite(ScratchAddress + 0x200, nameBytes));
        context[CpuRegister.Rdi] = reservedAddress;
        context[CpuRegister.Rsi] = 0x100000;
        context[CpuRegister.Rdx] = ScratchAddress + 0x200;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelSetVirtualRangeName(context));

        context[CpuRegister.Rdi] = reservedAddress;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.Rdx] = ScratchAddress + 0x100;
        context[CpuRegister.Rcx] = 72;
        Assert.Equal(0, KernelMemoryCompatExports.KernelVirtualQuery(context));
        Assert.True(memory.TryRead(ScratchAddress + 0x100, info));
        Assert.Equal(
            "unity-reserve",
            System.Text.Encoding.ASCII.GetString(info.Slice(33, 13)));
    }

    [Fact]
    public void Munmap_ReleasesRangeSplitByMprotect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const ulong rangeSize = 0x8000;
        using var memory = new PhysicalVirtualMemory();
        var rangeAddress = memory.AllocateAt(
            desiredAddress: 0,
            size: rangeSize,
            executable: false,
            allowAlternative: true);
        var context = new CpuContext(memory, Generation.Gen5);
        KernelMemoryCompatExports.TrackReservedVirtualRange(rangeAddress, rangeSize);

        context[CpuRegister.Rdi] = rangeAddress;
        context[CpuRegister.Rsi] = rangeSize / 2;
        context[CpuRegister.Rdx] = 0xF2;
        Assert.Equal(0, KernelMemoryCompatExports.KernelMprotect(context));

        context[CpuRegister.Rdi] = rangeAddress;
        context[CpuRegister.Rsi] = rangeSize;
        Assert.Equal(0, KernelMemoryCompatExports.KernelMunmap(context));
        Assert.False(memory.IsAccessible(rangeAddress, 1));
    }

    [Fact]
    public void TrackedVirtualRange_CanBeClaimedByFixedSubrange()
    {
        const ulong reservedAddress = 0x0000000700000000;
        const ulong reservedSize = 0x10000;
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        KernelMemoryCompatExports.TrackReservedVirtualRange(reservedAddress, reservedSize);

        Assert.True(
            KernelMemoryCompatExports.IsTrackedVirtualRange(
                reservedAddress + 0x4000,
                0x8000));
        Assert.False(
            KernelMemoryCompatExports.IsTrackedVirtualRange(
                reservedAddress + 0xC000,
                0x8000));

        Span<byte> requestedAddressBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(
            requestedAddressBytes,
            reservedAddress + 0x4000);
        Assert.True(memory.TryWrite(ScratchAddress, requestedAddressBytes));
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = ScratchAddress;
        context[CpuRegister.Rsi] = 0x8000;
        context[CpuRegister.Rdx] = 0xF2;
        context[CpuRegister.Rcx] = 0x10;
        context[CpuRegister.R8] = 0x200000;
        context[CpuRegister.R9] = 0x4000;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelMapDirectMemory(context));

        context[CpuRegister.Rdi] = reservedAddress + 0x4000;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.Rdx] = ScratchAddress + 0x100;
        context[CpuRegister.Rcx] = 72;
        Assert.Equal(0, KernelMemoryCompatExports.KernelVirtualQuery(context));
        Span<byte> info = stackalloc byte[72];
        Assert.True(memory.TryRead(ScratchAddress + 0x100, info));
        Assert.Equal(0x12, info[32] & 0x12);

        context[CpuRegister.Rdi] = reservedAddress + 0xC000;
        Assert.Equal(0, KernelMemoryCompatExports.KernelVirtualQuery(context));
        Assert.True(memory.TryRead(ScratchAddress + 0x100, info));
        Assert.Equal(reservedAddress + 0xC000, BinaryPrimitives.ReadUInt64LittleEndian(info));
        Assert.Equal(0, info[32] & 0x10);
    }

    [Fact]
    public void CheckedReleaseDirectMemory_DoesNotReleasePartiallyFreeRange()
    {
        const ulong searchStart = 0x00000003F0000000;
        var (context, allocatedAddress) = AllocateDirectMemory(
            searchStart,
            length: 0x8000);

        context[CpuRegister.Rdi] = allocatedAddress;
        context[CpuRegister.Rsi] = 0xC000;
        Assert.Equal(
            (int)OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));

        context[CpuRegister.Rsi] = 0x8000;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));
    }

    [Fact]
    public void CheckedReleaseDirectMemory_CanSplitAnAllocatedRange()
    {
        const ulong searchStart = 0x00000003F0100000;
        var (context, allocatedAddress) = AllocateDirectMemory(
            searchStart,
            length: 0xC000);

        context[CpuRegister.Rdi] = allocatedAddress + 0x4000;
        context[CpuRegister.Rsi] = 0x4000;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));

        context[CpuRegister.Rdi] = allocatedAddress;
        context[CpuRegister.Rsi] = 0xC000;
        Assert.Equal(
            (int)OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));

        context[CpuRegister.Rsi] = 0x4000;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));

        context[CpuRegister.Rdi] = allocatedAddress + 0x8000;
        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelCheckedReleaseDirectMemory(context));
    }

    [Fact]
    public void AllocateDirectMemory_ReturnsOutOfMemoryWhenRangeCannotFit()
    {
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 0;
        context[CpuRegister.Rsi] = 0x4000;
        context[CpuRegister.Rdx] = 0x8000;
        context[CpuRegister.Rcx] = 0x4000;
        context[CpuRegister.R8] = 0;
        context[CpuRegister.R9] = ScratchAddress;

        Assert.Equal(
            (int)OrbisGen2Result.ORBIS_GEN2_ERROR_OUT_OF_MEMORY,
            KernelMemoryCompatExports.KernelAllocateDirectMemory(context));
    }

    private static (CpuContext Context, ulong Address) AllocateDirectMemory(
        ulong searchStart,
        ulong length)
    {
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = searchStart;
        context[CpuRegister.Rsi] = searchStart + 0x10000;
        context[CpuRegister.Rdx] = length;
        context[CpuRegister.Rcx] = 0x4000;
        context[CpuRegister.R8] = 0;
        context[CpuRegister.R9] = ScratchAddress;

        Assert.Equal(
            0,
            KernelMemoryCompatExports.KernelAllocateDirectMemory(context));
        Span<byte> addressBytes = stackalloc byte[sizeof(ulong)];
        Assert.True(memory.TryRead(ScratchAddress, addressBytes));
        var allocatedAddress = BinaryPrimitives.ReadUInt64LittleEndian(addressBytes);
        Assert.Equal(searchStart, allocatedAddress);
        return (context, allocatedAddress);
    }
}
