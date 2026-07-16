// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using Xunit;

namespace SharpEmu.Libs.Tests.Pthread;

public sealed class PthreadKeyAllocationTests
{
    [Fact]
    public void PthreadKeyCreate_IsBoundedAndReusesDeletedKeys()
    {
        const ulong memoryBase = 0x1_0000_0000;
        const ulong outKeyAddress = memoryBase + 0x100;
        var memory = new FakeCpuMemory(memoryBase, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var allocatedKeys = new List<int>();

        try
        {
            for (var expectedKey = 1; expectedKey < 256; expectedKey++)
            {
                context[CpuRegister.Rdi] = outKeyAddress;
                context[CpuRegister.Rsi] = 0;

                Assert.Equal(0, KernelPthreadExtendedCompatExports.PosixPthreadKeyCreate(context));
                var actualKey = ReadInt32(memory, outKeyAddress);
                Assert.Equal(expectedKey, actualKey);
                allocatedKeys.Add(actualKey);
            }

            context[CpuRegister.Rdi] = outKeyAddress;
            context[CpuRegister.Rsi] = 0;
            Assert.Equal(
                (int)OrbisGen2Result.ORBIS_GEN2_ERROR_TRY_AGAIN,
                KernelPthreadExtendedCompatExports.PosixPthreadKeyCreate(context));

            const int releasedKey = 42;
            context[CpuRegister.Rdi] = releasedKey;
            Assert.Equal(0, KernelPthreadExtendedCompatExports.PosixPthreadKeyDelete(context));
            allocatedKeys.Remove(releasedKey);

            context[CpuRegister.Rdi] = outKeyAddress;
            context[CpuRegister.Rsi] = 0;
            Assert.Equal(0, KernelPthreadExtendedCompatExports.PosixPthreadKeyCreate(context));
            Assert.Equal(releasedKey, ReadInt32(memory, outKeyAddress));
            allocatedKeys.Add(releasedKey);
        }
        finally
        {
            foreach (var key in allocatedKeys)
            {
                context[CpuRegister.Rdi] = unchecked((ulong)key);
                KernelPthreadExtendedCompatExports.PosixPthreadKeyDelete(context);
            }
        }
    }

    private static int ReadInt32(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }
}
