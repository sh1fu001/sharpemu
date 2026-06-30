// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.IO;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using Xunit;

namespace SharpEmu.Tests;

public sealed class SelfLoaderTests
{
    private const int ElfHeaderSize = 64;
    private const int ProgramHeaderSize = 56;

    // Builds a minimal but valid little-endian ELF64 with a single PT_LOAD segment.
    private static byte[] BuildMinimalElf(byte[] segmentData, ulong entryAndVaddr = 0x1000)
    {
        var segmentOffset = ElfHeaderSize + ProgramHeaderSize;
        var buffer = new byte[segmentOffset + segmentData.Length];

        buffer[0] = 0x7F;
        buffer[1] = (byte)'E';
        buffer[2] = (byte)'L';
        buffer[3] = (byte)'F';
        buffer[4] = 2; // 64-bit
        buffer[5] = 1; // little-endian

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(16), 2);       // ET_EXEC
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(18), 0x3E);    // x86-64
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), 1);       // version
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(24), entryAndVaddr); // entry
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(32), ElfHeaderSize); // phoff
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(52), ElfHeaderSize);     // ehsize
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(54), ProgramHeaderSize); // phentsize
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(56), 1);                 // phnum

        var ph = ElfHeaderSize;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(ph + 0), 1);   // PT_LOAD
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(ph + 4), 5);   // R + X
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 8), (ulong)segmentOffset); // offset
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 16), entryAndVaddr);       // vaddr
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 24), entryAndVaddr);       // paddr
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 32), (ulong)segmentData.Length); // filesz
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 40), (ulong)segmentData.Length); // memsz
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(ph + 48), 0); // align (0 => no alignment check)

        segmentData.CopyTo(buffer, segmentOffset);
        return buffer;
    }

    [Fact]
    public void Load_EmptyImage_Throws()
    {
        var loader = new SelfLoader();
        var vm = new VirtualMemory();
        Assert.Throws<InvalidDataException>(() => loader.Load(Array.Empty<byte>(), vm));
    }

    [Fact]
    public void Load_TooSmallForElfHeader_Throws()
    {
        var loader = new SelfLoader();
        var vm = new VirtualMemory();
        Assert.Throws<InvalidDataException>(() => loader.Load(new byte[16], vm));
    }

    [Fact]
    public void Load_NonElfData_Throws()
    {
        var loader = new SelfLoader();
        var vm = new VirtualMemory();
        // 64 zero bytes: large enough for a header, but no ELF magic.
        Assert.Throws<InvalidDataException>(() => loader.Load(new byte[ElfHeaderSize], vm));
    }

    [Fact]
    public void Load_MinimalElf_ParsesAndMapsSegment()
    {
        var segment = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var elf = BuildMinimalElf(segment);

        var loader = new SelfLoader();
        var vm = new VirtualMemory();
        var image = loader.Load(elf, vm);

        Assert.False(image.IsSelf);
        Assert.Single(image.ProgramHeaders);
        Assert.Equal(ProgramHeaderType.Load, image.ProgramHeaders[0].HeaderType);
        Assert.NotEmpty(image.MappedRegions);

        // The LOAD segment maps at imageBase + vaddr, which equals image.EntryPoint
        // since vaddr == ELF entry point here.
        var readBack = new byte[segment.Length];
        Assert.True(vm.TryRead(image.EntryPoint, readBack));
        Assert.Equal(segment, readBack);
    }
}
