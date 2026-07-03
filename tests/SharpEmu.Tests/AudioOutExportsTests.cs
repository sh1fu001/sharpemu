// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Audio;
using Xunit;

namespace SharpEmu.Tests;

public sealed class AudioOutExportsTests
{
    [Fact]
    public void GetPortState_InitializesTheCompleteGuestStructure()
    {
        const ulong baseAddress = 0x0000000600040000;
        const ulong stateAddress = baseAddress + 0x100;
        var memory = new VirtualMemory();
        memory.Map(
            baseAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.R9] = 1;
        Assert.Equal(0, AudioOutExports.AudioOutOpen(context));
        var handle = context[CpuRegister.Rax];

        Span<byte> poison = stackalloc byte[0x20];
        poison.Fill(0xCC);
        Assert.True(memory.TryWrite(stateAddress, poison));

        context[CpuRegister.Rdi] = handle;
        context[CpuRegister.Rsi] = stateAddress;
        Assert.Equal(0, AudioOutExports.AudioOutGetPortState(context));

        Span<byte> state = stackalloc byte[0x20];
        Assert.True(memory.TryRead(stateAddress, state));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(state));
        Assert.Equal(2, state[0x02]);
        Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(state[0x04..]));
        Assert.DoesNotContain((byte)0xCC, state.ToArray());
    }
}
