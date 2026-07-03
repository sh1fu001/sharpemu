// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Generic;
using SharpEmu.Libs.Il2Cpp;
using Xunit;

namespace SharpEmu.Tests;

public sealed class Il2CppCodeRegistrationTests
{
    // A flat guest address space backed by a byte[], exposed through the bounds-checked reader the
    // scanner uses. Reads outside the populated span fail (as an unmapped page would).
    private sealed class FlatReader : IIl2CppMemoryReader
    {
        private readonly byte[] _memory;
        private readonly ulong _base;

        public FlatReader(ulong baseAddress, byte[] memory)
        {
            _base = baseAddress;
            _memory = memory;
        }

        public bool TryReadBytes(ulong address, System.Span<byte> buffer)
        {
            if (address < _base)
            {
                return false;
            }

            var offset = address - _base;
            if (offset + (ulong)buffer.Length > (ulong)_memory.Length)
            {
                return false;
            }

            _memory.AsSpan((int)offset, buffer.Length).CopyTo(buffer);
            return true;
        }
    }

    // Lays out, at a fixed base, a metadata registration whose fieldOffsets/typeDefinitionsSizes use
    // the array-of-structs + per-type-pointer encodings, with System.Object sized 0x10 and a couple
    // of real field offsets for a second type.
    private const ulong Base = 0x0000000800000000UL;
    private const int TypeCount = 3; // System.Object(0), System.String(1), GameManager(2)

    private static (byte[] Memory, ulong MetaRegVA) BuildImage()
    {
        // Layout inside the image (offsets from Base):
        //   0x0000  padding so nothing collides with a zero VA
        //   0x0100  typeDefinitionsSizes: TypeCount * 16-byte entries
        //   0x0200  fieldOffsets: TypeCount pointers -> per-type int32 arrays
        //   0x0300  type 2's field offsets (2 int32s)
        //   0x0400  types[] backing (just needs to be a valid pointer target)
        //   0x1000  Il2CppMetadataRegistration struct (0x80 bytes)
        var memory = new byte[0x2000];

        void W32(int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(off), v);
        void W64(int off, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan(off), v);

        const int typeDefSizesOff = 0x0100;
        // instance_size for each type: Object=0x10, String=0x14, GameManager=0x30.
        W32(typeDefSizesOff + 0 * 0x10, 0x10);
        W32(typeDefSizesOff + 1 * 0x10, 0x14);
        W32(typeDefSizesOff + 2 * 0x10, 0x30);

        const int type2FieldOffsetsOff = 0x0300;
        W32(type2FieldOffsetsOff + 0, 0x10); // field 0 at offset 0x10
        W32(type2FieldOffsetsOff + 4, 0x18); // field 1 at offset 0x18

        const int fieldOffsetsOff = 0x0200; // TypeCount pointers
        W64(fieldOffsetsOff + 0 * 8, 0);                                  // Object: no fields
        W64(fieldOffsetsOff + 1 * 8, 0);                                  // String: no managed fields
        W64(fieldOffsetsOff + 2 * 8, Base + type2FieldOffsetsOff);        // GameManager

        const int typesOff = 0x0400;

        const int metaRegOff = 0x1000;
        // typesCount at 0x30 must be >= TypeCount; a valid pointer at 0x38.
        W32(metaRegOff + 0x30, 8);
        W64(metaRegOff + 0x38, Base + typesOff);
        // fieldOffsetsCount / typeDefinitionsSizesCount both == TypeCount, 0x10 apart.
        W32(metaRegOff + 0x50, TypeCount);
        W64(metaRegOff + 0x58, Base + fieldOffsetsOff);
        W32(metaRegOff + 0x60, TypeCount);
        W64(metaRegOff + 0x68, Base + typeDefSizesOff);

        return (memory, Base + metaRegOff);
    }

    private static (byte[] Memory, ulong MetaRegVA) BuildPointerTableImage()
    {
        var (memory, metaRegVA) = BuildImage();

        const int typeDefSizesPointersOff = 0x0100;
        const int typeDefSizesEntriesOff = 0x0500;
        void W32(int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(off), v);
        void W64(int off, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan(off), v);

        for (var i = 0; i < TypeCount; i++)
        {
            var entryOff = typeDefSizesEntriesOff + i * 0x10;
            W64(typeDefSizesPointersOff + i * 8, Base + (ulong)entryOff);
        }

        W32(typeDefSizesEntriesOff + 0 * 0x10, 0x10);
        W32(typeDefSizesEntriesOff + 1 * 0x10, 0x14);
        W32(typeDefSizesEntriesOff + 2 * 0x10, 0x30);
        return (memory, metaRegVA);
    }

    // Minimal metadata stub exposing just what the scanner needs: the type count, System.Object's
    // index, and a field local-index lookup for GameManager.
    private static Il2CppMetadata BuildMetadata()
    {
        // Reuse the synthetic-metadata builder shape from Il2CppMetadataTests but with the three types
        // the image expects. We construct a valid v24 blob so FindTypeIndex/FindFieldLocalIndex work.
        var stringBlob = new List<byte>();
        var stringOffsets = new Dictionary<string, int>();
        int Intern(string s)
        {
            if (stringOffsets.TryGetValue(s, out var existing))
            {
                return existing;
            }

            var off = stringBlob.Count;
            stringOffsets[s] = off;
            stringBlob.AddRange(System.Text.Encoding.UTF8.GetBytes(s));
            stringBlob.Add(0);
            return off;
        }

        const int typeDefSize = 0x5C;
        const int fieldDefSize = 0x0C;

        var objName = Intern("Object");
        var systemNs = Intern("System");
        var strName = Intern("String");
        var gmName = Intern("GameManager");
        var emptyNs = Intern("");
        var f0 = Intern("health");
        var f1 = Intern("score");

        var fields = new byte[2 * fieldDefSize];
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(0), f0);
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(fieldDefSize), f1);

        var typeDefs = new byte[TypeCount * typeDefSize];
        void WriteType(int index, int name, int ns, int fieldStart, int fieldCount)
        {
            var o = index * typeDefSize;
            BinaryPrimitives.WriteInt32LittleEndian(typeDefs.AsSpan(o + 0x00), name);
            BinaryPrimitives.WriteInt32LittleEndian(typeDefs.AsSpan(o + 0x04), ns);
            BinaryPrimitives.WriteInt32LittleEndian(typeDefs.AsSpan(o + 0x24), fieldStart);
            BinaryPrimitives.WriteUInt16LittleEndian(typeDefs.AsSpan(o + 0x48), (ushort)fieldCount);
        }

        WriteType(0, objName, systemNs, -1, 0);
        WriteType(1, strName, systemNs, -1, 0);
        WriteType(2, gmName, emptyNs, 0, 2);

        const int headerPairs = 20;
        var headerSize = 8 + headerPairs * 8;
        var stringOff = headerSize;
        var fieldsOff = stringOff + stringBlob.Count;
        var typeDefsOff = fieldsOff + fields.Length;
        var data = new byte[typeDefsOff + typeDefs.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), 0xFAB11BAF);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 24);
        void WritePair(int index, int off, int size)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8 + index * 8), off);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8 + index * 8 + 4), size);
        }

        WritePair(2, stringOff, stringBlob.Count);
        WritePair(11, fieldsOff, fields.Length);
        WritePair(19, typeDefsOff, typeDefs.Length);
        stringBlob.CopyTo(data, stringOff);
        fields.CopyTo(data, fieldsOff);
        typeDefs.CopyTo(data, typeDefsOff);
        return Il2CppMetadata.FromBytes(data);
    }

    [Fact]
    public void TryLocate_FindsRegistrationAndReadsSizesAndOffsets()
    {
        var (memory, metaRegVA) = BuildImage();
        var reader = new FlatReader(Base, memory);
        var metadata = BuildMetadata();

        var codeReg = Il2CppCodeRegistration.TryLocate(reader, Base, Base + (ulong)memory.Length, metadata);

        Assert.NotNull(codeReg);
        Assert.Equal(metaRegVA, codeReg!.MetadataRegistrationAddress);
        Assert.Equal(0x10u, codeReg.ObjectInstanceSizeProbe);

        // Instance sizes are read back per type.
        Assert.Equal(0x10u, codeReg.GetInstanceSize(0));
        Assert.Equal(0x14u, codeReg.GetInstanceSize(1));
        Assert.Equal(0x30u, codeReg.GetInstanceSize(2));

        // Field offsets for GameManager come from its per-type pointer array.
        var health = metadata.FindFieldLocalIndex(2, "health");
        var score = metadata.FindFieldLocalIndex(2, "score");
        Assert.Equal(0, health);
        Assert.Equal(1, score);
        Assert.Equal(0x10, codeReg.GetFieldOffset(2, health));
        Assert.Equal(0x18, codeReg.GetFieldOffset(2, score));
    }

    [Fact]
    public void TryLocate_ReturnsNull_WhenSignatureAbsent()
    {
        // Zeroed image: no location with two TypeCount counts 0x10 apart.
        var reader = new FlatReader(Base, new byte[0x2000]);
        var metadata = BuildMetadata();

        var codeReg = Il2CppCodeRegistration.TryLocate(reader, Base, Base + 0x2000, metadata);

        Assert.Null(codeReg);
    }

    [Fact]
    public void TryLocate_ReadsPointerBasedTypeDefinitionSizes()
    {
        var (memory, _) = BuildPointerTableImage();
        var codeReg = Il2CppCodeRegistration.TryLocate(
            new FlatReader(Base, memory),
            Base,
            Base + (ulong)memory.Length,
            BuildMetadata());

        Assert.NotNull(codeReg);
        Assert.Equal(0x10u, codeReg!.GetInstanceSize(0));
        Assert.Equal(0x14u, codeReg.GetInstanceSize(1));
        Assert.Equal(0x30u, codeReg.GetInstanceSize(2));
        Assert.Equal(0u, codeReg.GetInstanceSize(TypeCount));
        Assert.Equal(-1, codeReg.GetFieldOffset(TypeCount, 0));
    }
}
