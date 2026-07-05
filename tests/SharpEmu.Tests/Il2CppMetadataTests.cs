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
        const int methodDefSize = 0x20;

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
        var methodNameOff = Intern("Start");
        var parameterNameOff = Intern("value");
        var propertyNameOff = Intern("Value");

        // Fields table: one field for type 1.
        var fields = new byte[1 * fieldDefSize];
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(0), fieldNameOff); // nameIndex
        BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(4), 0);            // typeIndex
        BinaryPrimitives.WriteUInt32LittleEndian(fields.AsSpan(8), 0);           // token

        var parameters = new byte[0x0C];
        BinaryPrimitives.WriteInt32LittleEndian(parameters.AsSpan(0x00), parameterNameOff);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(0x04), 0x08000001);
        BinaryPrimitives.WriteInt32LittleEndian(parameters.AsSpan(0x08), 0);

        var properties = new byte[0x14];
        BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(0x00), propertyNameOff);
        BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(0x04), 0);
        BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(0x08), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(properties.AsSpan(0x0C), 0x0200);
        BinaryPrimitives.WriteUInt32LittleEndian(properties.AsSpan(0x10), 0x17000001);

        var methods = new byte[methodDefSize];
        BinaryPrimitives.WriteInt32LittleEndian(methods.AsSpan(0x00), methodNameOff);
        BinaryPrimitives.WriteInt32LittleEndian(methods.AsSpan(0x04), 1); // declaring type
        BinaryPrimitives.WriteInt32LittleEndian(methods.AsSpan(0x08), 0); // return type
        BinaryPrimitives.WriteInt32LittleEndian(methods.AsSpan(0x0C), 0);
        BinaryPrimitives.WriteInt32LittleEndian(methods.AsSpan(0x10), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(methods.AsSpan(0x14), 0x06000001);
        BinaryPrimitives.WriteUInt16LittleEndian(methods.AsSpan(0x18), 0x0006);
        BinaryPrimitives.WriteUInt16LittleEndian(methods.AsSpan(0x1C), 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(methods.AsSpan(0x1E), 1);

        // Two type definitions: System.String and GameManager (GameManager owns field 0).
        var typeDefs = new byte[2 * typeDefSize];
        WriteTypeDef(typeDefs, 0, stringNameOff, systemNsOff, fieldStart: -1, fieldCount: 0, methodStart: -1, methodCount: 0, propertyStart: -1, propertyCount: 0);
        WriteTypeDef(typeDefs, 1, gmNameOff, emptyNsOff, fieldStart: 0, fieldCount: 1, methodStart: 0, methodCount: 1, propertyStart: 0, propertyCount: 1);

        // Header: magic, version, then 28 (offset,count) pairs. Only string(2), fields(11),
        // typeDefinitions(19) are populated; the rest point at an empty region.
        const int headerPairs = 28;
        var headerSize = 8 + headerPairs * 8;
        var stringOff = headerSize;
        var propertiesOff = stringOff + stringBlob.Count;
        var methodsOff = propertiesOff + properties.Length;
        var parametersOff = methodsOff + methods.Length;
        var fieldsOff = parametersOff + parameters.Length;
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
        WritePair(4, propertiesOff, properties.Length);
        WritePair(5, methodsOff, methods.Length);
        WritePair(10, parametersOff, parameters.Length);
        WritePair(11, fieldsOff, fields.Length);
        WritePair(19, typeDefsOff, typeDefs.Length);

        stringBlob.CopyTo(data, stringOff);
        properties.CopyTo(data, propertiesOff);
        methods.CopyTo(data, methodsOff);
        parameters.CopyTo(data, parametersOff);
        fields.CopyTo(data, fieldsOff);
        typeDefs.CopyTo(data, typeDefsOff);
        return data;

        static void WriteTypeDef(
            byte[] buffer,
            int index,
            int nameOff,
            int nsOff,
            int fieldStart,
            int fieldCount,
            int methodStart,
            int methodCount,
            int propertyStart,
            int propertyCount)
        {
            var o = index * typeDefSize;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x00), nameOff);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x04), nsOff);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x24), fieldStart);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x28), methodStart);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 0x30), propertyStart);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(o + 0x44), (ushort)methodCount);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(o + 0x46), (ushort)propertyCount);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(o + 0x48), (ushort)fieldCount);
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
    public void MethodMetadata_ResolvesDeclaredMethod()
    {
        var metadata = Il2CppMetadata.FromBytes(BuildSyntheticMetadata());

        Assert.Equal(1, metadata.MethodCount);
        Assert.Equal(1, metadata.GetMethodCount(1));
        Assert.Equal(0, metadata.GetMethodStart(1));
        Assert.Equal(0, metadata.FindMethodIndex(1, "Start", 1));
        Assert.Equal(-1, metadata.FindMethodIndex(1, "Start", 0));
        Assert.Equal("Start", metadata.GetMethodName(0));
        Assert.Equal(1, metadata.GetMethodDeclaringType(0));
        Assert.Equal(0x06000001u, metadata.GetMethodToken(0));
        Assert.Equal(1u, metadata.GetMethodParameterCount(0));
        Assert.Equal("value", metadata.GetMethodParameterName(0, 0));
        Assert.Equal(0, metadata.GetMethodParameterTypeIndex(0, 0));
        Assert.Equal(0x08000001u, metadata.GetMethodParameterToken(0, 0));
    }

    [Fact]
    public void PropertyMetadata_ResolvesAccessorAndAttributes()
    {
        var metadata = Il2CppMetadata.FromBytes(BuildSyntheticMetadata());

        Assert.Equal(1, metadata.PropertyCount);
        Assert.Equal(0, metadata.GetPropertyStart(1));
        Assert.Equal(1, metadata.GetPropertyCount(1));
        Assert.Equal("Value", metadata.GetPropertyName(0));
        Assert.Equal(0, metadata.GetPropertyGetMethodIndex(0));
        Assert.Equal(-1, metadata.GetPropertySetMethodIndex(0));
        Assert.Equal(0x0200u, metadata.GetPropertyAttributes(0));
        Assert.Equal(0x17000001u, metadata.GetPropertyToken(0));
    }

    [Fact]
    public void FromBytes_RejectsBadSignature()
    {
        var bytes = new byte[64];
        Assert.Throws<System.IO.InvalidDataException>(() => Il2CppMetadata.FromBytes(bytes));
    }
}
