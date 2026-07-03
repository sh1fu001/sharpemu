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
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);

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

    [Fact]
    public void KernelPread_ReadsAtOffsetWithoutChangingFilePosition()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempPath, "0123456789"u8.ToArray());

            var memory = new VirtualMemory();
            memory.Map(
                ScratchAddress,
                0x4000,
                0,
                ReadOnlySpan<byte>.Empty,
                ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
            var context = new CpuContext(memory, Generation.Gen5);

            WriteCString(memory, ScratchAddress, tempPath);
            context[CpuRegister.Rdi] = ScratchAddress;
            context[CpuRegister.Rsi] = 0;
            Assert.Equal(0, KernelMemoryCompatExports.KernelOpenUnderscore(context));
            var fd = context[CpuRegister.Rax];

            try
            {
                context[CpuRegister.Rdi] = fd;
                context[CpuRegister.Rsi] = 7;
                context[CpuRegister.Rdx] = 0;
                Assert.Equal(0, KernelMemoryCompatExports.KernelLseek(context));
                Assert.Equal(7UL, context[CpuRegister.Rax]);

                context[CpuRegister.Rdi] = fd;
                context[CpuRegister.Rsi] = ScratchAddress + 0x1000;
                context[CpuRegister.Rdx] = 4;
                context[CpuRegister.Rcx] = 2;
                Assert.Equal(0, KernelMemoryCompatExports.KernelPread(context));
                Assert.Equal(4UL, context[CpuRegister.Rax]);

                var actual = new byte[4];
                Assert.True(memory.TryRead(ScratchAddress + 0x1000, actual));
                Assert.Equal("2345"u8.ToArray(), actual);

                context[CpuRegister.Rdi] = fd;
                context[CpuRegister.Rsi] = 0;
                context[CpuRegister.Rdx] = 1;
                Assert.Equal(0, KernelMemoryCompatExports.KernelLseek(context));
                Assert.Equal(7UL, context[CpuRegister.Rax]);
            }
            finally
            {
                context[CpuRegister.Rdi] = fd;
                Assert.Equal(0, KernelMemoryCompatExports.KernelClose(context));
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Powf_AndLog2f_UseSysVFloatRegisters()
    {
        var context = new CpuContext(new VirtualMemory(), Generation.Gen5);
        context.SetXmmRegister(0, BitConverter.SingleToUInt32Bits(2.0f), 0);
        context.SetXmmRegister(1, BitConverter.SingleToUInt32Bits(8.0f), 0);

        Assert.Equal(0, KernelRuntimeCompatExports.Powf(context));
        Assert.Equal(256.0f, ReadFloatResult(context));

        context.SetXmmRegister(0, BitConverter.SingleToUInt32Bits(256.0f), 0);
        Assert.Equal(0, KernelRuntimeCompatExports.Log2f(context));
        Assert.Equal(8.0f, ReadFloatResult(context));
    }

    [Fact]
    public void KernelMprotect_AcceptsLazyReservedWindowsRange()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        const nuint size = 0x4000;
        var address = VirtualAlloc(0, size, MemReserve, PageReadWrite);
        Assert.NotEqual(0, address);
        try
        {
            var context = new CpuContext(new VirtualMemory(), Generation.Gen5);
            context[CpuRegister.Rdi] = unchecked((ulong)address);
            context[CpuRegister.Rsi] = size;
            context[CpuRegister.Rdx] = 0xF2;

            Assert.Equal(0, KernelMemoryCompatExports.KernelMprotect(context));
        }
        finally
        {
            Assert.True(VirtualFree(address, 0, MemRelease));
        }
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

    private static float ReadFloatResult(CpuContext context)
    {
        context.GetXmmRegister(0, out var low, out _);
        return BitConverter.Int32BitsToSingle(unchecked((int)low));
    }
}
