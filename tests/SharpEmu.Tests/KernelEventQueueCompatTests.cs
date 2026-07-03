// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using Xunit;

namespace SharpEmu.Tests;

public sealed class KernelEventQueueCompatTests
{
    private const ulong ScratchAddress = 0x10000;
    private const ulong EventAddress = ScratchAddress + 0x100;
    private const ulong CountAddress = ScratchAddress + 0x200;
    private const ulong TimeoutAddress = ScratchAddress + 0x300;

    [Fact]
    public void KernelWaitEqueue_ZeroTimeoutAndEmptyQueue_ReturnsTimedOut()
    {
        var (memory, context, handle) = CreateQueue();
        try
        {
            WriteUInt32(memory, CountAddress, uint.MaxValue);
            WriteUInt32(memory, TimeoutAddress, 0);
            SetWaitArguments(context, handle);

            Assert.Equal(
                (int)OrbisGen2Result.ORBIS_GEN2_ERROR_TIMED_OUT,
                KernelEventQueueCompatExports.KernelWaitEqueue(context));
            Assert.Equal(0U, ReadUInt32(memory, CountAddress));
        }
        finally
        {
            DeleteQueue(context, handle);
        }
    }

    [Fact]
    public void KernelWaitEqueue_ZeroTimeoutAndPendingEvent_DeliversEvent()
    {
        var (memory, context, handle) = CreateQueue();
        try
        {
            Assert.True(
                KernelEventQueueCompatExports.EnqueueEvent(
                    handle,
                    new KernelEventQueueCompatExports.KernelQueuedEvent(
                        0x12,
                        KernelEventQueueCompatExports.KernelEventFilterGraphics,
                        0x20,
                        3,
                        0x4567,
                        0x89AB)));
            WriteUInt32(memory, TimeoutAddress, 0);
            SetWaitArguments(context, handle);

            Assert.Equal(0, KernelEventQueueCompatExports.KernelWaitEqueue(context));
            Assert.Equal(1U, ReadUInt32(memory, CountAddress));
            Assert.Equal(0x12UL, ReadUInt64(memory, EventAddress));
            Assert.Equal(0x4567UL, ReadUInt64(memory, EventAddress + 0x10));
            Assert.Equal(0x89ABUL, ReadUInt64(memory, EventAddress + 0x18));
        }
        finally
        {
            DeleteQueue(context, handle);
        }
    }

    private static (VirtualMemory Memory, CpuContext Context, ulong Handle) CreateQueue()
    {
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = ScratchAddress;
        Assert.Equal(0, KernelEventQueueCompatExports.KernelCreateEqueue(context));
        var handle = ReadUInt64(memory, ScratchAddress);
        return (memory, context, handle);
    }

    private static void SetWaitArguments(CpuContext context, ulong handle)
    {
        context[CpuRegister.Rdi] = handle;
        context[CpuRegister.Rsi] = EventAddress;
        context[CpuRegister.Rdx] = 1;
        context[CpuRegister.Rcx] = CountAddress;
        context[CpuRegister.R8] = TimeoutAddress;
    }

    private static void DeleteQueue(CpuContext context, ulong handle)
    {
        context[CpuRegister.Rdi] = handle;
        Assert.Equal(0, KernelEventQueueCompatExports.KernelDeleteEqueue(context));
    }

    private static void WriteUInt32(VirtualMemory memory, ulong address, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Assert.True(memory.TryWrite(address, bytes));
    }

    private static uint ReadUInt32(VirtualMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static ulong ReadUInt64(VirtualMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }
}
