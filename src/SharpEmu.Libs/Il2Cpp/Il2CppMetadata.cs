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
    private const int TypeDef_ByValTypeIndex = 0x08;
    private const int TypeDef_DeclaringTypeIndex = 0x10;
    private const int TypeDef_ParentIndex = 0x14;
    private const int TypeDef_ElementTypeIndex = 0x18;
    private const int TypeDef_GenericContainerIndex = 0x1C;
    private const int TypeDef_FieldStart = 0x24;
    private const int TypeDef_MethodStart = 0x28;
    private const int TypeDef_NestedTypesStart = 0x34;
    private const int TypeDef_MethodCount = 0x44; // uint16 (v24.4)
    private const int TypeDef_FieldCount = 0x48; // uint16
    private const int TypeDef_NestedTypeCount = 0x4C; // uint16
    private const int TypeDef_Flags = 0x20;
    private const int TypeDef_Token = 0x58;

    // Il2CppMethodDefinition (v24.4) is 32 bytes.
    private const int MethodDefSize = 0x20;
    private const int MethodDef_NameIndex = 0x00;
    private const int MethodDef_DeclaringType = 0x04;
    private const int MethodDef_ReturnType = 0x08;
    private const int MethodDef_ParameterStart = 0x0C;
    private const int MethodDef_GenericContainerIndex = 0x10;
    private const int MethodDef_Token = 0x14;
    private const int MethodDef_Flags = 0x18; // uint16
    private const int MethodDef_IfFlags = 0x1A; // uint16
    private const int MethodDef_Slot = 0x1C; // uint16
    private const int MethodDef_ParameterCount = 0x1E; // uint16

    // Il2CppFieldDefinition (v24) is 12 bytes: nameIndex, typeIndex, token.
    private const int FieldDefSize = 0x0C;
    private const int FieldDef_NameIndex = 0x00;
    private const int FieldDef_TypeIndex = 0x04;

    // Il2CppStringLiteral (v24) is 8 bytes: { uint32 length; int32 dataIndex; }.
    private const int StringLiteralSize = 0x08;

    // Il2CppMetadataUsagePair (v24) is 8 bytes: { uint32 destinationIndex; uint32 encodedSourceIndex; }.
    private const int MetadataUsagePairSize = 0x08;

    // Unity 2019.4.39 (metadata v24.5) uses a 40-byte Il2CppImageDefinition.
    private const int ImageDefSize = 0x28;
    private const int ImageDef_NameIndex = 0x00;
    private const int ImageDef_AssemblyIndex = 0x04;
    private const int ImageDef_TypeStart = 0x08;
    private const int ImageDef_TypeCount = 0x0C;

    private const int CustomAttributeRangeSize = 0x0C;

    private readonly byte[] _data;
    private readonly int _stringOffset;
    private readonly int _typeDefsOffset;
    private readonly int _typeDefsCount;
    private readonly int _fieldsOffset;
    private readonly int _fieldsCount;
    private readonly int _methodsOffset;
    private readonly int _methodsCount;
    private readonly int _stringLiteralOffset;
    private readonly int _stringLiteralCount;
    private readonly int _stringLiteralDataOffset;
    private readonly int _metadataUsagePairsOffset;
    private readonly int _metadataUsagePairsCount;
    private readonly int _fieldRefsOffset;
    private readonly int _fieldRefsCount;
    private readonly int _nestedTypesOffset;
    private readonly int _nestedTypesCount;
    private readonly int _imagesOffset;
    private readonly int _imagesCount;
    private readonly int _attributeRangesOffset;
    private readonly int _attributeRangesCount;
    private readonly int _attributeTypesOffset;
    private readonly int _attributeTypesCount;
    private Dictionary<uint, (int Start, int Count)>? _attributeRangeByToken;

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
        _stringLiteralOffset = ReadHeaderInt(0);     // stringLiteral table (pair 0)
        _stringLiteralCount = ReadHeaderInt(1) / StringLiteralSize;
        _stringLiteralDataOffset = ReadHeaderInt(2);  // stringLiteralData blob (pair 1)
        _stringOffset = ReadHeaderInt(4);    // string blob (pair 2)
        _methodsOffset = ReadHeaderInt(10);  // methods (pair 5)
        _methodsCount = ReadHeaderInt(11) / MethodDefSize;
        _fieldsOffset = ReadHeaderInt(22);   // fields (pair 11)
        _fieldsCount = ReadHeaderInt(23) / FieldDefSize;
        _nestedTypesOffset = ReadHeaderInt(30); // nestedTypes (pair 15)
        _nestedTypesCount = ReadHeaderInt(31) / sizeof(int);
        _typeDefsOffset = ReadHeaderInt(38); // typeDefinitions (pair 19)
        _typeDefsCount = ReadHeaderInt(39) / TypeDefSize;
        _imagesOffset = ReadHeaderInt(40); // images (pair 20 in metadata v24.5)
        _imagesCount = ReadHeaderInt(41) / ImageDefSize;
        _metadataUsagePairsOffset = ReadHeaderInt(46); // metadataUsagePairs (pair 23)
        _metadataUsagePairsCount = ReadHeaderInt(47) / MetadataUsagePairSize;
        // Unity 2019.4 metadata v24.5 places fieldRefs immediately after metadataUsagePairs. A field
        // reference is { TypeIndex typeIndex; int32 fieldOrdinal; } and feeds usage kind 4.
        _fieldRefsOffset = ReadHeaderInt(48);
        _fieldRefsCount = ReadHeaderInt(49) / sizeof(long);
        _attributeRangesOffset = ReadHeaderInt(52); // attributesInfo (pair 26)
        _attributeRangesCount = ReadHeaderInt(53) / CustomAttributeRangeSize;
        _attributeTypesOffset = ReadHeaderInt(54); // attributeTypes (pair 27)
        _attributeTypesCount = ReadHeaderInt(55) / sizeof(int);

        BuildTypeIndex();
    }

    public int TypeCount => _typeDefsCount;

    public int MethodCount => _methodsCount;

    public int FieldCount => _fieldsCount;

    public int ImageCount => _imagesCount;

    /// <summary>Number of (destination, encodedSource) metadata-usage pairs the runtime must resolve.</summary>
    public int MetadataUsagePairCount => _metadataUsagePairsCount;

    /// <summary>Reads one metadata-usage pair; false if the index is out of range.</summary>
    public bool TryGetMetadataUsagePair(int index, out uint destinationIndex, out uint encodedSourceIndex)
    {
        destinationIndex = 0;
        encodedSourceIndex = 0;
        if ((uint)index >= (uint)_metadataUsagePairsCount)
        {
            return false;
        }

        var o = _metadataUsagePairsOffset + index * MetadataUsagePairSize;
        if (o < 0 || o + MetadataUsagePairSize > _data.Length)
        {
            return false;
        }

        destinationIndex = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(o));
        encodedSourceIndex = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(o + 4));
        return true;
    }

    /// <summary>Decodes a UTF-8 string-literal by index; empty string if the index is out of range.</summary>
    public string GetStringLiteral(int index)
    {
        if ((uint)index >= (uint)_stringLiteralCount)
        {
            return string.Empty;
        }

        var o = _stringLiteralOffset + index * StringLiteralSize;
        if (o < 0 || o + StringLiteralSize > _data.Length)
        {
            return string.Empty;
        }

        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(o));
        var dataIndex = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(o + 4));
        var start = _stringLiteralDataOffset + dataIndex;
        if (length < 0 || start < 0 || (long)start + length > _data.Length)
        {
            return string.Empty;
        }

        return System.Text.Encoding.UTF8.GetString(_data, start, length);
    }

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

    public int FindTypeIndex(int imageIndex, string @namespace, string name)
    {
        if ((uint)imageIndex >= (uint)_imagesCount || string.IsNullOrEmpty(name))
        {
            return -1;
        }

        var start = GetImageTypeStart(imageIndex);
        var count = GetImageTypeCount(imageIndex);
        for (var i = 0; i < count; i++)
        {
            var typeIndex = start + i;
            if ((uint)typeIndex >= (uint)_typeDefsCount)
            {
                break;
            }

            if (string.Equals(GetTypeName(typeIndex), name, StringComparison.Ordinal) &&
                string.Equals(GetTypeNamespace(typeIndex), @namespace, StringComparison.Ordinal))
            {
                return typeIndex;
            }
        }

        return -1;
    }

    public string GetImageName(int imageIndex)
    {
        if ((uint)imageIndex >= (uint)_imagesCount)
        {
            return string.Empty;
        }

        return ReadString(ReadInt(_imagesOffset + imageIndex * ImageDefSize + ImageDef_NameIndex));
    }

    public int GetImageAssemblyIndex(int imageIndex)
    {
        if ((uint)imageIndex >= (uint)_imagesCount)
        {
            return -1;
        }

        return ReadInt(_imagesOffset + imageIndex * ImageDefSize + ImageDef_AssemblyIndex);
    }

    public int GetImageTypeStart(int imageIndex)
    {
        if ((uint)imageIndex >= (uint)_imagesCount)
        {
            return -1;
        }

        return ReadInt(_imagesOffset + imageIndex * ImageDefSize + ImageDef_TypeStart);
    }

    public int GetImageTypeCount(int imageIndex)
    {
        if ((uint)imageIndex >= (uint)_imagesCount)
        {
            return 0;
        }

        return ReadInt(_imagesOffset + imageIndex * ImageDefSize + ImageDef_TypeCount);
    }

    public int FindImageIndexForType(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return -1;
        }

        for (var imageIndex = 0; imageIndex < _imagesCount; imageIndex++)
        {
            var start = GetImageTypeStart(imageIndex);
            var count = GetImageTypeCount(imageIndex);
            if (typeIndex >= start && typeIndex - start < count)
            {
                return imageIndex;
            }
        }

        return -1;
    }

    public int FindImageIndex(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return -1;
        }

        for (var i = 0; i < _imagesCount; i++)
        {
            var candidate = GetImageName(i);
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidate),
                    Path.GetFileNameWithoutExtension(name),
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
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

    public int GetTypeByValTypeIndex(int typeIndex) =>
        ReadTypeDefinitionInt(typeIndex, TypeDef_ByValTypeIndex);

    public int GetTypeDeclaringTypeIndex(int typeIndex) =>
        ReadTypeDefinitionInt(typeIndex, TypeDef_DeclaringTypeIndex);

    public int GetTypeParentTypeIndex(int typeIndex) =>
        ReadTypeDefinitionInt(typeIndex, TypeDef_ParentIndex);

    public int GetTypeElementTypeIndex(int typeIndex) =>
        ReadTypeDefinitionInt(typeIndex, TypeDef_ElementTypeIndex);

    public int GetTypeGenericContainerIndex(int typeIndex) =>
        ReadTypeDefinitionInt(typeIndex, TypeDef_GenericContainerIndex);

    public uint GetTypeToken(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(
            _data.AsSpan(_typeDefsOffset + typeIndex * TypeDefSize + TypeDef_Token));
    }

    public int GetNestedTypeCount(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(
            _data.AsSpan(_typeDefsOffset + typeIndex * TypeDefSize + TypeDef_NestedTypeCount));
    }

    public int GetNestedTypeIndex(int typeIndex, int ordinal)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount || ordinal < 0)
        {
            return -1;
        }

        var count = GetNestedTypeCount(typeIndex);
        var start = ReadTypeDefinitionInt(typeIndex, TypeDef_NestedTypesStart);
        if (ordinal >= count || start < 0 || start + ordinal >= _nestedTypesCount)
        {
            return -1;
        }

        return ReadInt(_nestedTypesOffset + (start + ordinal) * sizeof(int));
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

    public int GetMethodStart(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return -1;
        }

        return ReadInt(_typeDefsOffset + typeIndex * TypeDefSize + TypeDef_MethodStart);
    }

    public int GetMethodCount(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(
            _data.AsSpan(_typeDefsOffset + typeIndex * TypeDefSize + TypeDef_MethodCount));
    }

    public uint GetTypeFlags(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(
            _data.AsSpan(_typeDefsOffset + typeIndex * TypeDefSize + TypeDef_Flags));
    }

    public string GetMethodName(int methodIndex)
    {
        if ((uint)methodIndex >= (uint)_methodsCount)
        {
            return string.Empty;
        }

        return ReadString(ReadInt(_methodsOffset + methodIndex * MethodDefSize + MethodDef_NameIndex));
    }

    public int GetMethodDeclaringType(int methodIndex)
    {
        if ((uint)methodIndex >= (uint)_methodsCount)
        {
            return -1;
        }

        return ReadInt(_methodsOffset + methodIndex * MethodDefSize + MethodDef_DeclaringType);
    }

    public int GetMethodReturnTypeIndex(int methodIndex) =>
        ReadMethodInt(methodIndex, MethodDef_ReturnType);

    public int GetMethodParameterStart(int methodIndex) =>
        ReadMethodInt(methodIndex, MethodDef_ParameterStart);

    public int GetMethodGenericContainerIndex(int methodIndex) =>
        ReadMethodInt(methodIndex, MethodDef_GenericContainerIndex);

    public uint GetMethodToken(int methodIndex)
    {
        if ((uint)methodIndex >= (uint)_methodsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(
            _data.AsSpan(_methodsOffset + methodIndex * MethodDefSize + MethodDef_Token));
    }

    public ushort GetMethodFlags(int methodIndex) =>
        ReadMethodUInt16(methodIndex, MethodDef_Flags);

    public ushort GetMethodImplementationFlags(int methodIndex) =>
        ReadMethodUInt16(methodIndex, MethodDef_IfFlags);

    public ushort GetMethodSlot(int methodIndex) =>
        ReadMethodUInt16(methodIndex, MethodDef_Slot);

    public ushort GetMethodParameterCount(int methodIndex) =>
        ReadMethodUInt16(methodIndex, MethodDef_ParameterCount);

    public int FindMethodIndex(int typeIndex, string methodName, int argumentCount)
    {
        var start = GetMethodStart(typeIndex);
        var count = GetMethodCount(typeIndex);
        if (start < 0 || count == 0 || string.IsNullOrEmpty(methodName))
        {
            return -1;
        }

        for (var i = 0; i < count; i++)
        {
            var methodIndex = start + i;
            if ((uint)methodIndex >= (uint)_methodsCount)
            {
                break;
            }

            if (string.Equals(GetMethodName(methodIndex), methodName, StringComparison.Ordinal) &&
                (argumentCount < 0 || GetMethodParameterCount(methodIndex) == argumentCount))
            {
                return methodIndex;
            }
        }

        return -1;
    }

    /// <summary>Global index of the first field declared on this type (-1 if none/invalid).</summary>
    public int GetFieldStart(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return -1;
        }

        var o = _typeDefsOffset + typeIndex * TypeDefSize;
        return ReadInt(o + TypeDef_FieldStart);
    }

    /// <summary>
    /// Resolves a declared field's ordinal WITHIN its type (0-based, in declaration order). This is
    /// the index used against the binary's per-type <c>fieldOffsets[typeIndex]</c> array; returns -1
    /// if the field is not declared on the type.
    /// </summary>
    public int FindFieldLocalIndex(int typeIndex, string fieldName)
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
                return i;
            }
        }

        return -1;
    }

    public string GetFieldName(int fieldIndex)
    {
        if ((uint)fieldIndex >= (uint)_fieldsCount)
        {
            return string.Empty;
        }

        return ReadString(ReadInt(_fieldsOffset + fieldIndex * FieldDefSize + FieldDef_NameIndex));
    }

    public int GetFieldTypeIndex(int fieldIndex)
    {
        if ((uint)fieldIndex >= (uint)_fieldsCount)
        {
            return -1;
        }

        return ReadInt(_fieldsOffset + fieldIndex * FieldDefSize + FieldDef_TypeIndex);
    }

    public uint GetFieldToken(int fieldIndex)
    {
        if ((uint)fieldIndex >= (uint)_fieldsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(
            _data.AsSpan(_fieldsOffset + fieldIndex * FieldDefSize + 8));
    }

    /// <summary>
    /// Resolves a metadata field-reference index to its registered type index and field ordinal.
    /// </summary>
    public bool TryGetFieldReference(int referenceIndex, out int typeIndex, out int fieldOrdinal)
    {
        typeIndex = -1;
        fieldOrdinal = -1;
        if ((uint)referenceIndex >= (uint)_fieldRefsCount)
        {
            return false;
        }

        var offset = _fieldRefsOffset + referenceIndex * sizeof(long);
        if (offset < 0 || offset + sizeof(long) > _data.Length)
        {
            return false;
        }

        typeIndex = ReadInt(offset);
        fieldOrdinal = ReadInt(offset + sizeof(int));
        return typeIndex >= 0 && fieldOrdinal >= 0;
    }

    /// <summary>
    /// Returns the registered Il2CppType indices of the custom attributes attached to a metadata
    /// token (type, method, field, and so on).
    /// </summary>
    public IReadOnlyList<int> GetCustomAttributeTypeIndices(uint token)
    {
        EnsureAttributeRangeIndex();
        if (_attributeRangeByToken is null ||
            !_attributeRangeByToken.TryGetValue(token, out var range) ||
            range.Start < 0 ||
            range.Count <= 0 ||
            range.Start + range.Count > _attributeTypesCount)
        {
            return Array.Empty<int>();
        }

        var result = new int[range.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ReadInt(_attributeTypesOffset + (range.Start + i) * sizeof(int));
        }

        return result;
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

    private void EnsureAttributeRangeIndex()
    {
        if (_attributeRangeByToken is not null)
        {
            return;
        }

        var ranges = new Dictionary<uint, (int Start, int Count)>();
        for (var i = 0; i < _attributeRangesCount; i++)
        {
            var offset = _attributeRangesOffset + i * CustomAttributeRangeSize;
            if (offset < 0 || offset + CustomAttributeRangeSize > _data.Length)
            {
                break;
            }

            var token = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));
            var start = ReadInt(offset + sizeof(uint));
            var count = ReadInt(offset + sizeof(uint) + sizeof(int));
            if (token != 0 && start >= 0 && count > 0)
            {
                ranges[token] = (start, count);
            }
        }

        _attributeRangeByToken = ranges;
    }

    private int ReadHeaderInt(int uint32Index) =>
        BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(8 + uint32Index * 4));

    private int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(offset));

    private ushort ReadMethodUInt16(int methodIndex, int fieldOffset)
    {
        if ((uint)methodIndex >= (uint)_methodsCount)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(
            _data.AsSpan(_methodsOffset + methodIndex * MethodDefSize + fieldOffset));
    }

    private int ReadMethodInt(int methodIndex, int fieldOffset)
    {
        if ((uint)methodIndex >= (uint)_methodsCount)
        {
            return -1;
        }

        return ReadInt(_methodsOffset + methodIndex * MethodDefSize + fieldOffset);
    }

    private int ReadTypeDefinitionInt(int typeIndex, int fieldOffset)
    {
        if ((uint)typeIndex >= (uint)_typeDefsCount)
        {
            return -1;
        }

        return ReadInt(_typeDefsOffset + typeIndex * TypeDefSize + fieldOffset);
    }

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
