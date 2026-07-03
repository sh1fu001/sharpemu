// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using Xunit;

namespace SharpEmu.Tests;

public sealed class KernelSemaphoreCompatTests
{
    [Fact]
    public void Signal_PreparesBlockedGuestWaitContinuation()
    {
        const ulong baseAddress = 0x0000000600010000;
        const ulong handleAddress = baseAddress + 0x100;
        const ulong nameAddress = baseAddress + 0x200;
        const ulong returnSlotAddress = baseAddress + 0x300;
        var memory = new VirtualMemory();
        memory.Map(
            baseAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(nameAddress, "blocked-semaphore\0"u8));

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = handleAddress;
        context[CpuRegister.Rsi] = nameAddress;
        context[CpuRegister.Rdx] = 0;
        context[CpuRegister.Rcx] = 0;
        context[CpuRegister.R8] = 1;
        context[CpuRegister.R9] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelCreateSema(context));
        Span<byte> handleBytes = stackalloc byte[sizeof(uint)];
        Assert.True(memory.TryRead(handleAddress, handleBytes));
        var handle = BinaryPrimitives.ReadUInt32LittleEndian(handleBytes);

        var previousThread = GuestThreadExecution.EnterGuestThread(0x1234);
        var previousFrame = GuestThreadExecution.EnterImportCallFrame(
            0x8000_1000,
            baseAddress + 0x800,
            returnSlotAddress);
        try
        {
            context[CpuRegister.Rdi] = handle;
            context[CpuRegister.Rsi] = 1;
            context[CpuRegister.Rdx] = 0;
            Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));
            Assert.True(GuestThreadExecution.TryConsumeCurrentThreadBlock(
                out _,
                out _,
                out var hasContinuation,
                out var wakeKey,
                out var resumeHandler,
                out var wakeHandler));
            Assert.True(hasContinuation);
            Assert.Equal($"semaphore:0x{handle:X8}", wakeKey);
            Assert.NotNull(resumeHandler);
            Assert.NotNull(wakeHandler);
            Assert.False(wakeHandler!());

            context[CpuRegister.Rdi] = handle;
            context[CpuRegister.Rsi] = 1;
            Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));
            Assert.True(wakeHandler());
            Assert.Equal(0, resumeHandler!());
        }
        finally
        {
            GuestThreadExecution.RestoreImportCallFrame(previousFrame);
            GuestThreadExecution.RestoreGuestThread(previousThread);
        }
    }

    [Fact]
    public void Wait_AcceptsStaleFmodWrapperDuringTeardown()
    {
        const ulong baseAddress = 0x0000000600020000;
        const ulong handleAddress = baseAddress + 0x100;
        const ulong staleWrapperAddress = baseAddress + 0x180;
        const ulong nameAddress = baseAddress + 0x200;
        var memory = new VirtualMemory();
        memory.Map(
            baseAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(nameAddress, "FMOD Semaphore\0"u8));

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = handleAddress;
        context[CpuRegister.Rsi] = nameAddress;
        context[CpuRegister.Rdx] = 1;
        context[CpuRegister.Rcx] = 0;
        context[CpuRegister.R8] = 0xFFFF;
        context[CpuRegister.R9] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelCreateSema(context));

        context[CpuRegister.Rdi] = staleWrapperAddress;
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));

        context[CpuRegister.Rdi] = 0;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));

        Span<byte> handleBytes = stackalloc byte[sizeof(uint)];
        Assert.True(memory.TryRead(handleAddress, handleBytes));
        context[CpuRegister.Rdi] = BinaryPrimitives.ReadUInt32LittleEndian(handleBytes);
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));
    }

    [Fact]
    public void WaitAndSignal_AcceptPointerToSemaphoreHandle()
    {
        const ulong baseAddress = 0x0000000600000000;
        const ulong handleAddress = baseAddress + 0x100;
        const ulong indirectAddress = baseAddress + 0x108;
        const ulong nestedWrapperAddress = baseAddress + 0x110;
        const ulong nestedHandleAddress = baseAddress + 0x118;
        const ulong relativeWrapperAddress = baseAddress + 0x120;
        const ulong nameAddress = baseAddress + 0x200;
        var memory = new VirtualMemory();
        memory.Map(
            baseAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(nameAddress, "test-semaphore\0"u8));

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = handleAddress;
        context[CpuRegister.Rsi] = nameAddress;
        context[CpuRegister.Rdx] = 0;
        context[CpuRegister.Rcx] = 1;
        context[CpuRegister.R8] = 1;
        context[CpuRegister.R9] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelCreateSema(context));

        Span<byte> handleBytes = stackalloc byte[sizeof(uint)];
        Assert.True(memory.TryRead(handleAddress, handleBytes));
        Assert.True(memory.TryWrite(indirectAddress, handleBytes));

        context[CpuRegister.Rdi] = indirectAddress;
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));

        context[CpuRegister.Rdi] = indirectAddress;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));

        Assert.True(memory.TryWrite(nestedHandleAddress, handleBytes));
        Span<byte> nestedAddressBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(nestedAddressBytes, nestedHandleAddress);
        Assert.True(memory.TryWrite(nestedWrapperAddress, nestedAddressBytes));

        context[CpuRegister.Rdi] = nestedWrapperAddress;
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));

        Span<byte> relativeAddressBytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(relativeAddressBytes, unchecked((uint)handleAddress));
        Assert.True(memory.TryWrite(relativeWrapperAddress, relativeAddressBytes));

        context[CpuRegister.Rdi] = relativeWrapperAddress;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));

        Assert.True(memory.TryWrite(handleAddress, stackalloc byte[sizeof(uint)]));
        context[CpuRegister.Rdi] = relativeWrapperAddress;
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelWaitSema(context));

        var handle = BinaryPrimitives.ReadUInt32LittleEndian(handleBytes);
        context[CpuRegister.Rdi] = handle;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelDeleteSema(context));
    }

    [Fact]
    public void FmodSignal_IsHarmlessWhenSaturatedOrDeleted()
    {
        const ulong baseAddress = 0x0000000600030000;
        const ulong handleAddress = baseAddress + 0x100;
        const ulong nameAddress = baseAddress + 0x200;
        var memory = new VirtualMemory();
        memory.Map(
            baseAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(nameAddress, "FMOD Semaphore\0"u8));

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = handleAddress;
        context[CpuRegister.Rsi] = nameAddress;
        context[CpuRegister.Rdx] = 1;
        context[CpuRegister.Rcx] = 1;
        context[CpuRegister.R8] = 1;
        context[CpuRegister.R9] = 0;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelCreateSema(context));

        Span<byte> handleBytes = stackalloc byte[sizeof(uint)];
        Assert.True(memory.TryRead(handleAddress, handleBytes));
        var handle = BinaryPrimitives.ReadUInt32LittleEndian(handleBytes);

        context[CpuRegister.Rdi] = handle;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));

        Assert.Equal(0, KernelSemaphoreCompatExports.KernelDeleteSema(context));
        Assert.Equal(0, KernelSemaphoreCompatExports.KernelSignalSema(context));
    }
}
