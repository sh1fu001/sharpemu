// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SharpEmu.Core.Loader;
using Xunit;

namespace SharpEmu.Tests;

public sealed class ElfHeaderParsingTests
{
    private static byte[] BuildHeader()
    {
        var buffer = new byte[64];
        buffer[0] = 0x7F;
        buffer[1] = (byte)'E';
        buffer[2] = (byte)'L';
        buffer[3] = (byte)'F';
        buffer[4] = 2; // 64-bit
        buffer[5] = 1; // little-endian
        buffer[7] = 9; // ABI (arbitrary)

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(16), 0xFE00); // type
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(18), 0x3E);   // machine = x86-64
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), 1);      // version
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(24), 0x1234_5678_9ABC_DEF0UL); // entry
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(32), 0x40);   // phoff
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(40), 0x200);  // shoff
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(52), 64);     // ehsize
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(54), 56);     // phentsize
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(56), 7);      // phnum
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(60), 3);      // shnum
        return buffer;
    }

    [Fact]
    public void ValidHeader_ParsesAllFields()
    {
        var header = MemoryMarshal.Read<ElfHeader>(BuildHeader());

        Assert.True(header.HasElfMagic);
        Assert.True(header.Is64Bit);
        Assert.True(header.IsLittleEndian);
        Assert.Equal(0xFE00, header.Type);
        Assert.Equal(0x3E, header.Machine);
        Assert.Equal(1u, header.Version);
        Assert.Equal(0x1234_5678_9ABC_DEF0UL, header.EntryPoint);
        Assert.Equal(0x40UL, header.ProgramHeaderOffset);
        Assert.Equal(0x200UL, header.SectionHeaderOffset);
        Assert.Equal(64, header.HeaderSize);
        Assert.Equal(56, header.ProgramHeaderEntrySize);
        Assert.Equal(7, header.ProgramHeaderCount);
        Assert.Equal(3, header.SectionHeaderCount);
    }

    [Fact]
    public void HeaderStructSize_Is64Bytes()
    {
        Assert.Equal(64, Marshal.SizeOf<ElfHeader>());
    }

    [Fact]
    public void NonElfMagic_IsRejected()
    {
        var buffer = BuildHeader();
        buffer[0] = 0x00;
        var header = MemoryMarshal.Read<ElfHeader>(buffer);
        Assert.False(header.HasElfMagic);
    }
}
