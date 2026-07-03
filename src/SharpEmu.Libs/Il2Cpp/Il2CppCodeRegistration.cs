// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Bounds-checked reader over guest memory. In SharpEmu guest code runs directly in the host
/// process, but the loaded image can contain unmapped gaps, so all reads go through a safe
/// <c>TryRead</c> that returns false rather than faulting on an unmapped page.
/// </summary>
public interface IIl2CppMemoryReader
{
    /// <summary>Reads exactly <paramref name="buffer"/>.Length bytes; false if any byte is unmapped.</summary>
    bool TryReadBytes(ulong address, Span<byte> buffer);
}

/// <summary>
/// Locates and reads the <c>Il2CppMetadataRegistration</c> structure that the IL2CPP-compiled game
/// binary carries in its static data. Unlike <see cref="Il2CppMetadata"/> (the on-disk
/// <c>global-metadata.dat</c>), this table lives in the image and holds the two things generated
/// code and the engine need at runtime but that are NOT in the .dat: per-type <b>instance sizes</b>
/// and per-field <b>runtime offsets</b>. Reading garbage for these is the root cause of the boot
/// crashes (an unpopulated size/offset flows into an allocation length or a string length).
///
/// The structure is found by scanning the loaded module for its signature: two 32-bit counts,
/// <c>fieldOffsetsCount</c> and <c>typeDefinitionsSizesCount</c>, both equal to the number of type
/// definitions and 0x10 bytes apart, each followed by a pointer into the image. The exact on-disk
/// encoding of the two tables varies slightly across IL2CPP builds, so their format (array-of-structs
/// vs array-of-pointers; per-type-pointer vs flat) is auto-detected by probing anchor types whose
/// sizes are known a priori (<c>System.Object</c> has instance size 0x10).
/// </summary>
public sealed class Il2CppCodeRegistration
{
    // Il2CppMetadataRegistration field byte offsets (64-bit; each int32 count is padded to an
    // 8-byte slot and followed by an 8-byte pointer).
    private const int OffTypesCount = 0x30;
    private const int OffTypesPtr = 0x38;
    private const int OffFieldOffsetsCount = 0x50;
    private const int OffFieldOffsetsPtr = 0x58;
    private const int OffTypeDefSizesCount = 0x60;
    private const int OffTypeDefSizesPtr = 0x68;
    private const int StructSize = 0x80;

    // Il2CppTypeDefinitionSizes: { uint32 instance_size; int32 native_size; uint32 static_fields_size;
    // uint32 thread_static_fields_size; } = 16 bytes.
    private const int TypeDefSizesEntrySize = 0x10;

    private readonly IIl2CppMemoryReader _reader;
    private readonly ulong _fieldOffsetsPtr;
    private readonly ulong _typeDefSizesPtr;
    private readonly bool _fieldOffsetsArePointers;
    private readonly bool _typeDefSizesArePointers;
    private readonly int _typeCount;

    /// <summary>Virtual address of the located Il2CppMetadataRegistration structure.</summary>
    public ulong MetadataRegistrationAddress { get; }

    /// <summary>Instance size (bytes) of System.Object as read back — 0x10 when parsing is correct.</summary>
    public uint ObjectInstanceSizeProbe { get; }

    private Il2CppCodeRegistration(
        IIl2CppMemoryReader reader,
        ulong metadataRegistration,
        ulong fieldOffsetsPtr,
        ulong typeDefSizesPtr,
        bool fieldOffsetsArePointers,
        bool typeDefSizesArePointers,
        int typeCount,
        uint objectInstanceSizeProbe)
    {
        _reader = reader;
        MetadataRegistrationAddress = metadataRegistration;
        _fieldOffsetsPtr = fieldOffsetsPtr;
        _typeDefSizesPtr = typeDefSizesPtr;
        _fieldOffsetsArePointers = fieldOffsetsArePointers;
        _typeDefSizesArePointers = typeDefSizesArePointers;
        _typeCount = typeCount;
        ObjectInstanceSizeProbe = objectInstanceSizeProbe;
    }

    /// <summary>
    /// Scans <c>[moduleBase, moduleEnd)</c> for the metadata registration matching
    /// <paramref name="metadata"/> and returns a reader for it, or null if not found / unreliable.
    /// </summary>
    public static Il2CppCodeRegistration? TryLocate(
        IIl2CppMemoryReader reader,
        ulong moduleBase,
        ulong moduleEnd,
        Il2CppMetadata metadata)
    {
        if (reader is null || moduleEnd <= moduleBase)
        {
            return null;
        }

        var typeCount = (uint)metadata.TypeCount;
        if (typeCount == 0)
        {
            return null;
        }

        if (!TryScan(reader, moduleBase, moduleEnd, typeCount, out var metaReg))
        {
            return null;
        }

        if (!TryReadU64(reader, metaReg + OffFieldOffsetsPtr, out var fieldOffsetsPtr) ||
            !TryReadU64(reader, metaReg + OffTypeDefSizesPtr, out var typeDefSizesPtr))
        {
            return null;
        }

        // Anchor the format auto-detection on System.Object, whose instance size is always 0x10.
        var objectIndex = metadata.FindTypeIndex("System", "Object");
        if (objectIndex < 0)
        {
            return null;
        }

        if (!TryDetectTypeDefSizesFormat(reader, typeDefSizesPtr, objectIndex, out var tdsArePointers, out var objSize))
        {
            return null;
        }

        var foArePointers = DetectFieldOffsetsArePointers(reader, fieldOffsetsPtr, moduleBase, moduleEnd, (int)typeCount);

        return new Il2CppCodeRegistration(
            reader,
            metaReg,
            fieldOffsetsPtr,
            typeDefSizesPtr,
            foArePointers,
            tdsArePointers,
            (int)typeCount,
            objSize);
    }

    /// <summary>Instance size (bytes) for a type, or 0 if unavailable.</summary>
    public uint GetInstanceSize(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)_typeCount)
        {
            return 0;
        }

        if (_typeDefSizesArePointers)
        {
            if (!TryReadU64(_reader, _typeDefSizesPtr + (ulong)typeIndex * 8, out var entry) ||
                !TryReadU32(_reader, entry, out var size))
            {
                return 0;
            }

            return size;
        }

        return TryReadU32(_reader, _typeDefSizesPtr + (ulong)typeIndex * TypeDefSizesEntrySize, out var s) ? s : 0;
    }

    /// <summary>Runtime byte offset of a field given its owning type and local ordinal, or -1.</summary>
    public int GetFieldOffset(int typeIndex, int localFieldIndex)
    {
        if ((uint)typeIndex >= (uint)_typeCount || localFieldIndex < 0)
        {
            return -1;
        }

        if (_fieldOffsetsArePointers)
        {
            if (!TryReadU64(_reader, _fieldOffsetsPtr + (ulong)typeIndex * 8, out var typeOffsets) ||
                typeOffsets == 0 ||
                !TryReadU32(_reader, typeOffsets + (ulong)localFieldIndex * 4, out var off))
            {
                return -1;
            }

            return unchecked((int)off);
        }

        // Flat form: not indexable by (type, localField) without a global field index; unsupported.
        return -1;
    }

    private static bool TryDetectTypeDefSizesFormat(
        IIl2CppMemoryReader reader,
        ulong typeDefSizesPtr,
        int objectIndex,
        out bool arePointers,
        out uint objectInstanceSize)
    {
        arePointers = false;
        objectInstanceSize = 0;

        // Array-of-structs: instance_size is the first uint32 of the objectIndex'th 16-byte entry.
        var structOk = TryReadU32(reader, typeDefSizesPtr + (ulong)objectIndex * TypeDefSizesEntrySize, out var structSize);

        // Array-of-pointers: element is a pointer to the Il2CppTypeDefinitionSizes.
        uint pointerSize = 0;
        var pointerOk = TryReadU64(reader, typeDefSizesPtr + (ulong)objectIndex * 8, out var entryPtr) &&
                        TryReadU32(reader, entryPtr, out pointerSize);

        const uint ObjectSize = 0x10;
        if (structOk && structSize == ObjectSize)
        {
            arePointers = false;
            objectInstanceSize = structSize;
            return true;
        }

        if (pointerOk && pointerSize == ObjectSize)
        {
            arePointers = true;
            objectInstanceSize = pointerSize;
            return true;
        }

        // Accept a plausible (non-zero, sane) struct-form size even if not exactly 0x10 (the anchor
        // type layout can differ by build), but reject obvious garbage.
        if (structOk && structSize is > 0 and < 0x10000)
        {
            arePointers = false;
            objectInstanceSize = structSize;
            return true;
        }

        return false;
    }

    private static bool DetectFieldOffsetsArePointers(
        IIl2CppMemoryReader reader,
        ulong fieldOffsetsPtr,
        ulong moduleBase,
        ulong moduleEnd,
        int typeCount)
    {
        // In the per-type-pointer form each entry is either null (type has no fields) or a pointer
        // back into the image; in the flat form entries are small byte offsets. A single entry that
        // points into the module proves the pointer form. Types with no fields have a null entry, so
        // probe across types rather than trusting entry[0].
        var probe = Math.Min(typeCount, 8192);
        for (var i = 0; i < probe; i++)
        {
            if (TryReadU64(reader, fieldOffsetsPtr + (ulong)i * 8, out var entry) &&
                entry >= moduleBase && entry < moduleEnd)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryScan(
        IIl2CppMemoryReader reader,
        ulong moduleBase,
        ulong moduleEnd,
        uint typeCount,
        out ulong metadataRegistration)
    {
        metadataRegistration = 0;

        // Scan page-aligned so each backing read stays within a single guest page. A rolling window
        // large enough to hold a whole struct lets a candidate straddle the page it starts in.
        const int Window = 0x8000;
        var window = new byte[Window];
        var pos = moduleBase & ~0xFFFUL;

        while (pos < moduleEnd)
        {
            var want = (int)Math.Min((ulong)Window, moduleEnd - pos);
            var got = ReadContiguous(reader, pos, window, want);
            if (got < StructSize)
            {
                // Unmapped page or too little left; step past it.
                pos += got >= 0x1000 ? (ulong)(got & ~0xFFF) : 0x1000UL;
                continue;
            }

            var scanLimit = got - StructSize;
            for (var off = 0; off <= scanLimit; off += 8)
            {
                var c1 = BinaryPrimitives.ReadUInt32LittleEndian(window.AsSpan(off + OffFieldOffsetsCount));
                if (c1 != typeCount)
                {
                    continue;
                }

                var c2 = BinaryPrimitives.ReadUInt32LittleEndian(window.AsSpan(off + OffTypeDefSizesCount));
                if (c2 != typeCount)
                {
                    continue;
                }

                var candidate = pos + (ulong)off;
                if (Validate(window.AsSpan(off, StructSize), reader, moduleBase, moduleEnd, typeCount))
                {
                    metadataRegistration = candidate;
                    return true;
                }
            }

            // Advance, keeping enough overlap that a struct spanning the tail is not skipped.
            pos += (ulong)(got - (StructSize - 8)) & ~7UL;
        }

        return false;
    }

    private static bool Validate(
        ReadOnlySpan<byte> s,
        IIl2CppMemoryReader reader,
        ulong moduleBase,
        ulong moduleEnd,
        uint typeCount)
    {
        var typesCount = BinaryPrimitives.ReadUInt32LittleEndian(s[OffTypesCount..]);
        // Distinct Il2CppType* count is always >= number of type definitions (and not absurdly large).
        if (typesCount < typeCount || typesCount > typeCount * 16u + 0x100000u)
        {
            return false;
        }

        var typesPtr = BinaryPrimitives.ReadUInt64LittleEndian(s[OffTypesPtr..]);
        var fieldOffsetsPtr = BinaryPrimitives.ReadUInt64LittleEndian(s[OffFieldOffsetsPtr..]);
        var typeDefSizesPtr = BinaryPrimitives.ReadUInt64LittleEndian(s[OffTypeDefSizesPtr..]);

        return PointsIntoModule(typesPtr, moduleBase, moduleEnd) &&
               PointsIntoModule(fieldOffsetsPtr, moduleBase, moduleEnd) &&
               PointsIntoModule(typeDefSizesPtr, moduleBase, moduleEnd) &&
               TryReadU64(reader, fieldOffsetsPtr, out _) &&
               TryReadU64(reader, typeDefSizesPtr, out _);
    }

    private static bool PointsIntoModule(ulong pointer, ulong moduleBase, ulong moduleEnd) =>
        pointer >= moduleBase && pointer < moduleEnd;

    private static int ReadContiguous(IIl2CppMemoryReader reader, ulong address, byte[] buffer, int maxLength)
    {
        var total = 0;
        while (total < maxLength)
        {
            var chunk = Math.Min(0x1000, maxLength - total);
            if (!reader.TryReadBytes(address + (ulong)total, buffer.AsSpan(total, chunk)))
            {
                break;
            }

            total += chunk;
        }

        return total;
    }

    private static bool TryReadU32(IIl2CppMemoryReader reader, ulong address, out uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        if (!reader.TryReadBytes(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }

    private static bool TryReadU64(IIl2CppMemoryReader reader, ulong address, out ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        if (!reader.TryReadBytes(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return true;
    }
}
