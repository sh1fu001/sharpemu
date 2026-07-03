// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SharpEmu.Libs.Il2Cpp;
using Xunit;

namespace SharpEmu.Tests;

public sealed class Il2CppMetadataTests
{
    // Builds a minimal but structurally valid version-24 global-metadata.dat in memory:
    // a string blob plus a fields table and two type definitions, enough to exercise the parser's
    // header/string/type/field reading without depending on the (gitignored) real game asset.
    private static byte[] BuildSyntheticMetadata()
    {
        const int typeDefSize = 0x5C;
        const int fieldDefSize = 0x0C;

        // String blob: collect names and hand back their byte offsets.
        var stringBlob = new List<byte>();
        var stringOffsets = new Dictionary<string, int>();
        int Intern(string s)
        {
            if (stringOffsets.TryGetValue(s, out var off))
            {
                return off;
            }

            off = stringBlob.Count;
            stringOffsets[s] = off;
            stringBlob.AddRange(Encoding.UTF8.GetBytes(s));
            stringBlob.Add(0);
            return off;
        }

        var stringNameOff = Intern("String");
        var systemNsOff = Intern("System");
        var gmNameOff = Intern("GameManager");
        var emptyNsOff = Intern("");
        var fieldNameOff = Intern("m_value");

        // Fields table: one field for type 1.
        var fields = new byte[1 * fieldDefSize];
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(0), fieldNameOff); // nameIndex
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(4), 0);            // typeIndex
        BinaryPrimitives.WriteUInt32LittleEndian(fields.AsSpan(8), 0);           // token

        // Two type definitions: System.String and GameManager (GameManager owns field 0).
        var typeDefs = new byte[2 * typeDefSize];
        WriteTypeDef(typeDefs, 0, stringNameOff, systemNsOff, fieldStart: -1, fieldCount: 0);
        WriteTypeDef(typeDefs, 1, gmNameOff, emptyNsOff, fieldStart: 0, fieldCount: 1);

        // Header: magic, version, then 20 (offset,count) pairs. Only string(2), fields(11),
        // typeDefinitions(19) are populated; the rest point at an empty region.
        const int headerPairs = 20;
        var headerSize = 8 + headerPairs * 8;
        var stringOff = headerSize;
        var fieldsOff = stringOff + stringBlob.Count;
        var typeDefsOff = fieldsOff + fields.Length;
        var total = typeDefsOff + typeDefs.Length;

        var data = new byte[total];
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
        return data;

        static void WriteTypeDef(byte[] buffer, int index, int nameOff, int nsOff, int fieldStart, int fieldCount)
        {
            var o = index * typeDefSize;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x00), nameOff);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x04), nsOff);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x24), fieldStart);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(o + 0x4C), (ushort)fieldCount);
        }
    }

    [Fact]
    public void FromBytes_ParsesTypeNamesAndNamespaces()
    {
        var metadata = Il2CppMetadata.FromBytes(BuildSyntheticMetadata());

        Assert.Equal(2, metadata.TypeCount);
        Assert.Equal("String", metadata.GetTypeName(0));
        Assert.Equal("System", metadata.GetTypeNamespace(0));
        Assert.Equal("GameManager", metadata.GetTypeName(1));
        Assert.Equal(string.Empty, metadata.GetTypeNamespace(1));
    }

    [Fact]
    public void FindTypeIndex_ResolvesByFullNameAndBareName()
    {
        var metadata = Il2CppMetadata.FromBytes(BuildSyntheticMetadata());

        Assert.Equal(0, metadata.FindTypeIndex("System", "String"));
        Assert.Equal(0, metadata.FindTypeIndex(string.Empty, "String"));
        Assert.Equal(1, metadata.FindTypeIndex(string.Empty, "GameManager"));
        Assert.Equal(-1, metadata.FindTypeIndex("System", "DoesNotExist"));
    }

    [Fact]
    public void FindFieldIndex_LocatesDeclaredField()
    {
        var metadata = Il2CppMetadata.FromBytes(BuildSyntheticMetadata());

        Assert.Equal(1, metadata.GetFieldCount(1));
        Assert.Equal(0, metadata.FindFieldIndex(1, "m_value"));
        Assert.Equal(-1, metadata.FindFieldIndex(1, "missing"));
        Assert.Equal(-1, metadata.FindFieldIndex(0, "m_value"));
    }

    [Fact]
    public void FromBytes_RejectsBadSignature()
    {
        var bytes = new byte[64];
        Assert.Throws<System.IO.InvalidDataException>(() => Il2CppMetadata.FromBytes(bytes));
    }
}
