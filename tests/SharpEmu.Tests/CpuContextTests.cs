// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Tests;

public sealed class CpuContextTests
{
    private static CpuContext CreateContextWithStack(out VirtualMemory memory, ulong stackBase = 0x10000, ulong stackSize = 0x1000)
    {
        memory = new VirtualMemory();
        memory.Map(stackBase, stackSize, 0, ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        return new CpuContext(memory, Generation.Gen5);
    }

    [Fact]
    public void RegisterIndexer_RoundTrips()
    {
        var ctx = CreateContextWithStack(out _);
        ctx[CpuRegister.R12] = 0xDEADBEEFCAFEF00DUL;
        Assert.Equal(0xDEADBEEFCAFEF00DUL, ctx[CpuRegister.R12]);
        Assert.Equal(0UL, ctx[CpuRegister.R13]);
    }

    [Fact]
    public void RaxWriteFlag_TracksWritesAndClears()
    {
        var ctx = CreateContextWithStack(out _);
        Assert.False(ctx.WasRaxWritten);

        ctx[CpuRegister.Rbx] = 1;
        Assert.False(ctx.WasRaxWritten); // other registers do not set the flag

        ctx[CpuRegister.Rax] = 5;
        Assert.True(ctx.WasRaxWritten);

        ctx.ClearRaxWriteFlag();
        Assert.False(ctx.WasRaxWritten);
    }

    [Fact]
    public void TryWriteReadUInt64_RoundTrips()
    {
        var ctx = CreateContextWithStack(out _);
        Assert.True(ctx.TryWriteUInt64(0x10100, 0x0102030405060708UL));
        Assert.True(ctx.TryReadUInt64(0x10100, out var value));
        Assert.Equal(0x0102030405060708UL, value);
    }

    [Fact]
    public void TryReadUInt64_Unmapped_ReturnsFalse()
    {
        var ctx = CreateContextWithStack(out _);
        Assert.False(ctx.TryReadUInt64(0xFFFF_0000, out var value));
        Assert.Equal(0UL, value);
    }

    [Fact]
    public void PushPop_RoundTripsAndRestoresRsp()
    {
        var ctx = CreateContextWithStack(out _);
        ctx[CpuRegister.Rsp] = 0x10800;

        Assert.True(ctx.PushUInt64(0xAABBCCDDEEFF0011UL));
        Assert.Equal(0x107F8UL, ctx[CpuRegister.Rsp]);

        Assert.True(ctx.PopUInt64(out var value));
        Assert.Equal(0xAABBCCDDEEFF0011UL, value);
        Assert.Equal(0x10800UL, ctx[CpuRegister.Rsp]);
    }

    [Fact]
    public void XmmRegister_RoundTrips()
    {
        var ctx = CreateContextWithStack(out _);
        ctx.SetXmmRegister(3, 0x1111_2222_3333_4444UL, 0x5555_6666_7777_8888UL);
        ctx.GetXmmRegister(3, out var low, out var high);
        Assert.Equal(0x1111_2222_3333_4444UL, low);
        Assert.Equal(0x5555_6666_7777_8888UL, high);
    }

    [Fact]
    public void XmmRegister_OutOfRange_Throws()
    {
        var ctx = CreateContextWithStack(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => ctx.SetXmmRegister(16, 0, 0));
    }
}
