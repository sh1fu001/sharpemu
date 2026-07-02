// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using Xunit;

namespace SharpEmu.Tests;

public sealed class KernelLibcCompatTests
{
    private const ulong ScratchAddress = 0x10000;

    [Fact]
    public void Getpctype_ReturnsStableAsciiClassificationTable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var memory = new PhysicalVirtualMemory();
        var context = new CpuContext(memory, Generation.Gen5);

        Assert.Equal(0, KernelMemoryCompatExports.Getpctype(context));
        var tableAddress = context[CpuRegister.Rax];
        Assert.NotEqual(0UL, tableAddress);
        Assert.Equal((ushort)0x0081, ReadEntry(memory, tableAddress, 'A'));
        Assert.Equal((ushort)0x0082, ReadEntry(memory, tableAddress, 'f'));
        Assert.Equal((ushort)0x0084, ReadEntry(memory, tableAddress, '7'));
        Assert.Equal((ushort)0x0048, ReadEntry(memory, tableAddress, ' '));

        Assert.Equal(0, KernelMemoryCompatExports.Getpctype(context));
        Assert.Equal(tableAddress, context[CpuRegister.Rax]);
    }

    [Fact]
    public void Setenv_AndGetenv_RoundTripGuestValue()
    {
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x4000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        var context = new CpuContext(memory, Generation.Gen5);
        var name = $"SHARPEMU_TEST_{Guid.NewGuid():N}";
        WriteCString(memory, ScratchAddress, name);
        WriteCString(memory, ScratchAddress + 0x100, "guest-value");

        context[CpuRegister.Rdi] = ScratchAddress;
        context[CpuRegister.Rsi] = ScratchAddress + 0x100;
        context[CpuRegister.Rdx] = 1;
        Assert.Equal(0, KernelExports.Setenv(context));
        Assert.Equal(0UL, context[CpuRegister.Rax]);

        context[CpuRegister.Rdi] = ScratchAddress;
        Assert.Equal(0, KernelExports.Getenv(context));
        Assert.Equal(
            "guest-value",
            Marshal.PtrToStringAnsi(unchecked((nint)context[CpuRegister.Rax])));
    }

    private static ushort ReadEntry(PhysicalVirtualMemory memory, ulong tableAddress, char value)
    {
        Span<byte> entry = stackalloc byte[sizeof(ushort)];
        Assert.True(memory.TryRead(tableAddress + ((ulong)value * sizeof(ushort)), entry));
        return BinaryPrimitives.ReadUInt16LittleEndian(entry);
    }

    private static void WriteCString(VirtualMemory memory, ulong address, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + '\0');
        Assert.True(memory.TryWrite(address, bytes));
    }
}
