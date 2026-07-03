// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Parser for a Unity IL2CPP <c>global-metadata.dat</c> file (format version 24, as shipped by
/// PPSA09804/GRIS). This reads the on-disk metadata tables (strings, type/field/method definitions)
/// that describe the managed type system. It intentionally does NOT read the code-registration
/// tables from the game binary (method pointers, runtime field offsets); those live in the compiled
/// image and are a separate, larger piece of work. See <see cref="Il2CppRuntime"/> for how this feeds
/// the <c>il2cpp_*</c> HLE surface.
/// </summary>
public sealed class Il2CppMetadata
{
    private const uint ExpectedSanity = 0xFAB11BAF;
    private const int SupportedVersion = 24;

    // Il2CppTypeDefinition (v24) is 0x5C bytes; the fields this HLE needs sit at these offsets.
    private const int TypeDefSize = 0x5C;
    private const int TypeDef_NameIndex = 0x00;
    private const int TypeDef_NamespaceIndex = 0x04;
    private const int TypeDef_FieldStart = 0x24;
    private const int TypeDef_MethodStart = 0x28;
    private const int TypeDef_FieldCount = 0x4C; // uint16
    private const int TypeDef_MethodCount = 0x48; // uint16
    private const int TypeDef_Token = 0x58;

    // Il2CppFieldDefinition (v24) is 12 bytes: nameIndex, typeIndex, token.
    private const int FieldDefSize = 0x0C;
    private const int FieldDef_NameIndex = 0x00;
    private const int FieldDef_TypeIndex = 0x04;

    private readonly byte[] _data;
    private readonly int _stringOffset;
    private readonly int _typeDefsOffset;
    private readonly int _typeDefsCount;
    private readonly int _fieldsOffset;
    private readonly int _fieldsCount;

    // "Namespace.Name" and bare "Name" -> first matching type index.
    private readonly Dictionary<string, int> _typeByFullName = new(StringComparer.Ordinal);

    private Il2CppMetadata(byte[] data)
    {
        _data = data;
        var version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
        if (version != SupportedVersion)
        {
            throw new NotSupportedException($"Unsupported IL2CPP metadata version {version} (expected {SupportedVersion}).");
        }

        // Header is magic, version, then (offset,count) uint32 pairs. Pair N's offset is at uint32
        // index 2*N and its size at 2*N+1. Indices below are for version 24.
        _stringOffset = ReadHeaderInt(4);    // string blob (pair 2)
        _fieldsOffset = ReadHeaderInt(22);   // fields (pair 11)
        _fieldsCount = ReadHeaderInt(23) / FieldDefSize;
        _typeDefsOffset = ReadHeaderInt(38); // typeDefinitions (pair 19)
        _typeDefsCount = ReadHeaderInt(39) / TypeDefSize;

        BuildTypeIndex();
    }

    public int TypeCount => _typeDefsCount;

    public static Il2CppMetadata Load(string path) => FromBytes(File.ReadAllBytes(path));

    internal static Il2CppMetadata FromBytes(byte[] data)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(data) != ExpectedSanity)
        {
            throw new InvalidDataException("global-metadata.dat has an invalid signature.");
        }

        return new Il2CppMetadata(data);
    }

    /// <summary>Resolves a type index by "Namespace.Name" (or bare "Name"); returns -1 if unknown.</summary>
    public int FindTypeIndex(string @namespace, string name)
    {
        var full = string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
        if (_typeByFullName.TryGetValue(full, out var index))
        {
            return index;
        }

        return _typeByFullName.TryGetValue(name, out index) ? index : -1;
    }

    public string GetTypeName(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return string.Empty;
        }

        var o = _typeDefsOffset + typeIndex * TypeDefSize;
        return ReadString(ReadInt(o + TypeDef_NameIndex));
    }

    public string GetTypeNamespace(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return string.Empty;
        }

        var o = _typeDefsOffset + typeIndex * TypeDefSize;
        return ReadString(ReadInt(o + TypeDef_NamespaceIndex));
    }

    /// <summary>Number of instance+static fields declared directly on this type.</summary>
    public int GetFieldCount(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return 0;
        }

        var o = _typeDefsOffset + typeIndex * TypeDefSize;
        return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(o + TypeDef_FieldCount));
    }

    /// <summary>Finds a declared field's name index within a type; returns the global field index or -1.</summary>
    public int FindFieldIndex(int typeIndex, string fieldName)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return -1;
        }

        var o = _typeDefsOffset + typeIndex * TypeDefSize;
        var fieldStart = ReadInt(o + TypeDef_FieldStart);
        var fieldCount = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(o + TypeDef_FieldCount));
        if (fieldStart < 0)
        {
            return -1;
        }

        for (var i = 0; i < fieldCount; i++)
        {
            var globalIndex = fieldStart + i;
            if (globalIndex >= _fieldsCount)
            {
                break;
            }

            var fo = _fieldsOffset + globalIndex * FieldDefSize;
            if (string.Equals(ReadString(ReadInt(fo + FieldDef_NameIndex)), fieldName, StringComparison.Ordinal))
            {
                return globalIndex;
            }
        }

        return -1;
    }

    private void BuildTypeIndex()
    {
        for (var i = 0; i < _typeDefsCount; i++)
        {
            var name = GetTypeName(i);
            if (name.Length == 0)
            {
                continue;
            }

            var ns = GetTypeNamespace(i);
            var full = ns.Length == 0 ? name : ns + "." + name;
            _typeByFullName.TryAdd(full, i);
            _typeByFullName.TryAdd(name, i);
        }
    }

    private int ReadHeaderInt(int uint32Index) =>
        BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(8 + uint32Index * 4));

    private int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(offset));

    private string ReadString(int stringIndex)
    {
        if (stringIndex < 0)
        {
            return string.Empty;
        }

        var start = _stringOffset + stringIndex;
        if ((uint)start >= (uint)_data.Length)
        {
            return string.Empty;
        }

        var end = start;
        while (end < _data.Length && _data[end] != 0)
        {
            end++;
        }

        return System.Text.Encoding.UTF8.GetString(_data, start, end - start);
    }
}
