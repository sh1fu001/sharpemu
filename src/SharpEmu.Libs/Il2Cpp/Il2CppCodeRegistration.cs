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

    /// <summary>Writes exactly <paramref name="buffer"/>.Length bytes; false if the range is not writable.</summary>
    bool TryWriteBytes(ulong address, ReadOnlySpan<byte> buffer);
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
    private const int OffMethodSpecsCount = 0x40;
    private const int OffMethodSpecsPtr = 0x48;
    private const int OffFieldOffsetsCount = 0x50;
    private const int OffFieldOffsetsPtr = 0x58;
    private const int OffTypeDefSizesCount = 0x60;
    private const int OffTypeDefSizesPtr = 0x68;
    private const int OffMetadataUsagesCount = 0x70;
    private const int OffMetadataUsagesPtr = 0x78;
    private const int StructSize = 0x80;

    // Il2CppType (v24) is 16 bytes: { void* data; uint bitfield }. The 8-bit "type" kind sits at bit
    // 16 of the bitfield; for TYPE_CLASS/TYPE_VALUETYPE the low 32 bits of data are the type-def index.
    private const int Il2CppTypeSize = 0x10;
    private const uint Il2CppTypeClass = 0x12;
    private const uint Il2CppTypeValueType = 0x11;

    // Il2CppTypeDefinitionSizes: { uint32 instance_size; int32 native_size; uint32 static_fields_size;
    // uint32 thread_static_fields_size; } = 16 bytes.
    private const int TypeDefSizesEntrySize = 0x10;

    private readonly IIl2CppMemoryReader _reader;
    private readonly ulong _fieldOffsetsPtr;
    private readonly ulong _typeDefSizesPtr;
    private readonly ulong _typesPtr;
    private readonly int _typesRegistrationCount;
    private readonly ulong _methodSpecsPtr;
    private readonly int _methodSpecsCount;
    private readonly ulong _metadataUsagesPtr;
    private readonly int _metadataUsagesCount;
    private readonly bool _fieldOffsetsArePointers;
    private readonly bool _typeDefSizesArePointers;
    private readonly int _typeCount;
    private Dictionary<ulong, int>? _typeIndexByPointer;

    /// <summary>Virtual address of the located Il2CppMetadataRegistration structure.</summary>
    public ulong MetadataRegistrationAddress { get; }

    /// <summary>Instance size (bytes) of System.Object as read back — 0x10 when parsing is correct.</summary>
    public uint ObjectInstanceSizeProbe { get; }

    /// <summary>Number of metadataUsages slots the runtime is expected to fill (0 if none/unavailable).</summary>
    public int MetadataUsagesCount => _metadataUsagesCount;

    private Il2CppCodeRegistration(
        IIl2CppMemoryReader reader,
        ulong metadataRegistration,
        ulong fieldOffsetsPtr,
        ulong typeDefSizesPtr,
        ulong typesPtr,
        int typesRegistrationCount,
        ulong methodSpecsPtr,
        int methodSpecsCount,
        ulong metadataUsagesPtr,
        int metadataUsagesCount,
        bool fieldOffsetsArePointers,
        bool typeDefSizesArePointers,
        int typeCount,
        uint objectInstanceSizeProbe)
    {
        _reader = reader;
        MetadataRegistrationAddress = metadataRegistration;
        _fieldOffsetsPtr = fieldOffsetsPtr;
        _typeDefSizesPtr = typeDefSizesPtr;
        _typesPtr = typesPtr;
        _typesRegistrationCount = typesRegistrationCount;
        _methodSpecsPtr = methodSpecsPtr;
        _methodSpecsCount = methodSpecsCount;
        _metadataUsagesPtr = metadataUsagesPtr;
        _metadataUsagesCount = metadataUsagesCount;
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

        // typesCount / types[] and metadataUsages[] feed the metadata-usage resolution pass. A missing
        // metadataUsages table is fine (older/AOT builds); the fields degrade to "no usages".
        TryReadU32(reader, metaReg + OffTypesCount, out var typesRegCount);
        TryReadU64(reader, metaReg + OffTypesPtr, out var typesPtr);
        TryReadU32(reader, metaReg + OffMethodSpecsCount, out var methodSpecsCount);
        TryReadU64(reader, metaReg + OffMethodSpecsPtr, out var methodSpecsPtr);
        TryReadU64(reader, metaReg + OffMetadataUsagesCount, out var usagesCount);
        TryReadU64(reader, metaReg + OffMetadataUsagesPtr, out var usagesPtr);
        var usagesInModule = usagesPtr >= moduleBase && usagesPtr < moduleEnd;

        return new Il2CppCodeRegistration(
            reader,
            metaReg,
            fieldOffsetsPtr,
            typeDefSizesPtr,
            typesPtr,
            (int)typesRegCount,
            methodSpecsPtr,
            (int)Math.Min(methodSpecsCount, (uint)int.MaxValue),
            usagesInModule ? usagesPtr : 0,
            usagesInModule ? (int)Math.Min(usagesCount, int.MaxValue) : 0,
            foArePointers,
            tdsArePointers,
            (int)typeCount,
            objSize);
    }

    /// <summary>Reads the destination slot address (a void*) for a metadataUsages index, or 0.</summary>
    public bool TryGetUsageSlot(int destinationIndex, out ulong slotAddress)
    {
        slotAddress = 0;
        if (_metadataUsagesPtr == 0 || (uint)destinationIndex >= (uint)_metadataUsagesCount)
        {
            return false;
        }

        return TryReadU64(_reader, _metadataUsagesPtr + (ulong)destinationIndex * 8, out slotAddress) && slotAddress != 0;
    }

    /// <summary>Reads the Il2CppType* pointer at a types[] index, or 0.</summary>
    public ulong GetTypePointer(int typeIndex)
    {
        if (_typesPtr == 0 || (uint)typeIndex >= (uint)_typesRegistrationCount)
        {
            return 0;
        }

        return TryReadU64(_reader, _typesPtr + (ulong)typeIndex * 8, out var typePtr) ? typePtr : 0;
    }

    /// <summary>Returns a types[] registration index for an Il2CppType pointer, or -1.</summary>
    public int FindTypeRegistrationIndex(ulong typePointer)
    {
        if (typePointer == 0 || _typesPtr == 0)
        {
            return -1;
        }

        if (_typeIndexByPointer is null)
        {
            var index = new Dictionary<ulong, int>();
            for (var i = 0; i < _typesRegistrationCount; i++)
            {
                var pointer = GetTypePointer(i);
                if (pointer != 0)
                {
                    index.TryAdd(pointer, i);
                }
            }

            _typeIndexByPointer = index;
        }

        return _typeIndexByPointer.TryGetValue(typePointer, out var result) ? result : -1;
    }

    public uint GetTypeAttributes(int typeIndex)
    {
        var typePointer = GetTypePointer(typeIndex);
        return typePointer != 0 && TryReadU32(_reader, typePointer + 8, out var bitfield)
            ? bitfield & 0xFFFFu
            : 0;
    }

    public uint GetTypeKind(int typeIndex)
    {
        var typePointer = GetTypePointer(typeIndex);
        return typePointer != 0 && TryReadU32(_reader, typePointer + 8, out var bitfield)
            ? (bitfield >> 16) & 0xFFu
            : 0;
    }

    /// <summary>
    /// For a types[] index that denotes a plain class/value type, returns its type-definition index
    /// (so the caller can resolve an Il2CppClass); -1 for other type kinds or on any read failure.
    /// </summary>
    public int TryGetClassTypeDefinitionIndex(int typeIndex)
    {
        var typePtr = GetTypePointer(typeIndex);
        if (typePtr == 0 ||
            !TryReadU64(_reader, typePtr, out var data) ||
            !TryReadU32(_reader, typePtr + 8, out var bitfield))
        {
            return -1;
        }

        var kind = (bitfield >> 16) & 0xFF;
        if (kind != Il2CppTypeClass && kind != Il2CppTypeValueType)
        {
            return -1;
        }

        return unchecked((int)(uint)data);
    }

    /// <summary>
    /// Resolves an <c>Il2CppMethodSpec</c> index to its non-inflated method-definition index.
    /// Method specs are 12-byte triples whose first member is MethodDefinitionIndex.
    /// </summary>
    public int TryGetMethodDefinitionIndexFromSpec(int methodSpecIndex)
    {
        if (_methodSpecsPtr == 0 || (uint)methodSpecIndex >= (uint)_methodSpecsCount)
        {
            return -1;
        }

        return TryReadU32(
            _reader,
            _methodSpecsPtr + (ulong)methodSpecIndex * 12,
            out var methodDefinitionIndex)
            ? unchecked((int)methodDefinitionIndex)
            : -1;
    }

    /// <summary>Writes a resolved pointer into a metadataUsages destination slot.</summary>
    public bool TryWriteSlot(ulong slotAddress, ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return _reader.TryWriteBytes(slotAddress, buffer);
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
