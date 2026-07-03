// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Host-side IL2CPP runtime backing the <c>il2cpp_*</c> HLE surface with real data parsed from the
/// game's <c>global-metadata.dat</c> (see <see cref="Il2CppMetadata"/>). Because SharpEmu executes
/// guest code directly in the host process, host-allocated structures are dereferenceable by guest
/// code, so class handles / string / array objects allocated here can be handed straight to the game.
///
/// Scope: this provides real class identity (name/namespace lookup) and correctly-shaped
/// System.String / array objects (whose length fields the compiled game reads inline — the source of
/// the "garbage length" boot crashes). It does not yet provide runtime field offsets or method
/// pointers, which require parsing the game binary's code-registration tables.
/// </summary>
public sealed class Il2CppRuntime
{
    // Il2CppObject header (64-bit): { Il2CppClass* klass; void* monitor; } = 16 bytes.
    private const int ObjectHeaderSize = 16;
    private const int ArrayHeaderSize = 0x20;
    private const int ArrayLengthOffset = 0x18;
    private const ulong MaxArrayLength = 0x1000_0000;

    // Well-established version-24 Il2CppClass field offsets used for inline reads by generated code.
    private const int Class_Name = 0x10;
    private const int Class_Namespace = 0x18;
    private const int ClassBlockSize = 0x160; // over-allocated + zeroed; unknown fields read as 0.

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");
    private static readonly Lazy<Il2CppRuntime> _instance = new(() => new Il2CppRuntime());

    public static Il2CppRuntime Instance => _instance.Value;

    // Il2CppFieldInfo (v24): { const char* name; const Il2CppType* type; Il2CppClass* parent;
    // int32_t offset; uint32_t token; } — a small, stable struct. Populating it lets both
    // il2cpp_field_get_offset and any inline read of field->offset see the real runtime offset.
    private const int FieldInfoSize = 0x20;
    private const int FieldInfo_Name = 0x00;
    private const int FieldInfo_Parent = 0x10;
    private const int FieldInfo_Offset = 0x18;

    // MethodInfo layout used by v24-era generated code. Pointer-bearing fields are left null until
    // code-registration method pointers are mapped; identity/name/class/token/count are real.
    private const int MethodInfoSize = 0x50;
    private const int MethodInfo_MethodPointer = 0x00;
    private const int MethodInfo_Name = 0x10;
    private const int MethodInfo_Class = 0x18;
    private const int MethodInfo_Token = 0x40;
    private const int MethodInfo_Flags = 0x44;
    private const int MethodInfo_IfFlags = 0x46;
    private const int MethodInfo_Slot = 0x48;
    private const int MethodInfo_ParameterCount = 0x4A;

    private readonly object _gate = new();
    private readonly Dictionary<string, nint> _internalCalls = new(StringComparer.Ordinal);
    private readonly Il2CppMetadata? _metadata;
    private readonly Dictionary<int, nint> _classByTypeIndex = new();
    private readonly Dictionary<nint, int> _typeIndexByClass = new();
    private readonly Dictionary<nint, nint> _classNamePtr = new();
    private readonly Dictionary<nint, nint> _classNamespacePtr = new();
    private readonly Dictionary<(nint Class, string Field), nint> _fieldInfoByName = new();
    private readonly HashSet<nint> _ownedFieldInfos = new();
    private readonly Dictionary<int, nint> _methodByIndex = new();
    private readonly Dictionary<nint, int> _methodIndexByHandle = new();
    private readonly Dictionary<(nint ElementClass, uint Rank, bool Bounded), nint> _arrayClassByShape = new();
    private readonly Dictionary<nint, nuint> _arrayElementSizeByClass = new();
    private readonly Dictionary<nint, (ulong Length, ulong ByteLength)> _ownedArrays = new();

    private nint _stringClass;
    private bool _stringClassResolved;
    private nint _emptyCString;

    // A shared, never-null pointer to "" so name/namespace getters can degrade an unknown class to an
    // empty name instead of NULL. Generated reflection code often builds a std::string from a returned
    // name pointer as [p, p+strlen(p)); a NULL there yields a huge negative length and an OOB write.
    private nint GetEmptyCStringLocked() => _emptyCString != 0 ? _emptyCString : (_emptyCString = AllocateCString(string.Empty));

    // Binary code-registration (per-type instance sizes + field offsets); located lazily once guest
    // memory is reachable (see Attach).
    private Il2CppCodeRegistration? _codeRegistration;
    private bool _codeRegistrationAttempted;
    private IIl2CppMemoryReader? _reader;
    private ulong _moduleBase;
    private ulong _moduleEnd;

    public bool MetadataAvailable => _metadata is not null;

    public bool CodeRegistrationAvailable => _codeRegistration is not null;

    private Il2CppRuntime()
    {
        var path = ResolveMetadataPath();
        if (path is null || !File.Exists(path))
        {
            Log.Warning("global-metadata.dat not found; il2cpp class metadata is unavailable.");
            return;
        }

        try
        {
            _metadata = Il2CppMetadata.Load(path);
            Log.Info($"Loaded IL2CPP metadata from '{path}' ({_metadata.TypeCount} types).");
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load IL2CPP metadata: {ex.GetType().Name}: {ex.Message}");
            _metadata = null;
        }
    }

    // Opaque, dereferenceable-safe singleton handles for the IL2CPP runtime object graph the game
    // walks right after il2cpp_init (domain, corlib image, opened assemblies). They only need to be
    // non-null and readable-as-zero so the game treats init as having succeeded and keeps a stable
    // token to pass back into other il2cpp_* calls (which resolve work by name, not by these fields).
    private const int OpaqueHandleSize = 0x100;

    private readonly Dictionary<string, nint> _opaqueHandles = new(StringComparer.Ordinal);

    /// <summary>Returns a stable, non-null, zeroed handle for a named runtime object (allocated once).</summary>
    public unsafe nint GetOpaqueHandle(string key)
    {
        lock (_gate)
        {
            if (_opaqueHandles.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var handle = (nint)NativeMemory.AllocZeroed(OpaqueHandleSize);
            _opaqueHandles[key] = handle;
            return handle;
        }
    }

    /// <summary>Registers a native function as the implementation of a managed internal call (icall).</summary>
    public void RegisterInternalCall(string name, nint method)
    {
        if (string.IsNullOrEmpty(name) || method == 0)
        {
            return;
        }

        lock (_gate)
        {
            _internalCalls[name] = method;
        }
    }

    /// <summary>Returns the native pointer registered for an icall name, or 0 if none.</summary>
    public nint ResolveInternalCall(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        lock (_gate)
        {
            return _internalCalls.TryGetValue(name, out var method) ? method : 0;
        }
    }

    /// <summary>Resolves a class handle for a "Namespace/Name" pair, or 0 if unknown/unavailable.</summary>
    public nint GetClassFromName(string @namespace, string name)
    {
        if (_metadata is null)
        {
            return 0;
        }

        var typeIndex = _metadata.FindTypeIndex(@namespace, name);
        if (typeIndex < 0)
        {
            return 0;
        }

        return GetOrCreateClass(typeIndex);
    }

    public nint GetClassName(nint classHandle)
    {
        if (classHandle == 0 || _metadata is null)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_classNamePtr.TryGetValue(classHandle, out var namePtr))
            {
                return namePtr;
            }

            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return GetEmptyCStringLocked();
            }

            namePtr = AllocateCString(_metadata.GetTypeName(typeIndex));
            _classNamePtr[classHandle] = namePtr;
            return namePtr;
        }
    }

    public nint GetClassNamespace(nint classHandle)
    {
        if (classHandle == 0 || _metadata is null)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_classNamespacePtr.TryGetValue(classHandle, out var nsPtr))
            {
                return nsPtr;
            }

            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return GetEmptyCStringLocked();
            }

            nsPtr = AllocateCString(_metadata.GetTypeNamespace(typeIndex));
            _classNamespacePtr[classHandle] = nsPtr;
            return nsPtr;
        }
    }

    /// <summary>
    /// Supplies guest-memory access and the loaded main-module range so the binary code-registration
    /// (instance sizes + field offsets) can be located. Idempotent and cheap after the first success;
    /// safe to call from several il2cpp entry points (whichever the game reaches first wins).
    /// </summary>
    public void Attach(IIl2CppMemoryReader reader, ulong moduleBase, ulong moduleEnd)
    {
        if (reader is null || moduleEnd <= moduleBase || _metadata is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_codeRegistrationAttempted)
            {
                return;
            }

            _codeRegistrationAttempted = true;
            _reader = reader;
            _moduleBase = moduleBase;
            _moduleEnd = moduleEnd;

            try
            {
                _codeRegistration = Il2CppCodeRegistration.TryLocate(reader, moduleBase, moduleEnd, _metadata);
            }
            catch (Exception ex)
            {
                Log.Warning($"IL2CPP code-registration scan failed: {ex.GetType().Name}: {ex.Message}");
                _codeRegistration = null;
            }

            if (_codeRegistration is not null)
            {
                Log.Info(
                    $"Located Il2CppMetadataRegistration @0x{_codeRegistration.MetadataRegistrationAddress:X16} " +
                    $"(System.Object instance_size=0x{_codeRegistration.ObjectInstanceSizeProbe:X}).");
            }
            else
            {
                Log.Warning(
                    $"Il2CppMetadataRegistration not found in module [0x{moduleBase:X}, 0x{moduleEnd:X}); " +
                    "instance sizes and field offsets remain unavailable.");
            }
        }
    }

    /// <summary>Instance size (bytes) for a class handle, or 0 if unavailable.</summary>
    public uint GetInstanceSize(nint classHandle)
    {
        lock (_gate)
        {
            if (_codeRegistration is null || !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            return _codeRegistration.GetInstanceSize(typeIndex);
        }
    }

    /// <summary>
    /// Resolves a field by name on a class and returns a populated Il2CppFieldInfo handle (with the
    /// real runtime offset), or 0 if unavailable.
    /// </summary>
    public unsafe nint GetFieldFromName(nint classHandle, string fieldName)
    {
        if (classHandle == 0 || string.IsNullOrEmpty(fieldName) || _metadata is null)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_fieldInfoByName.TryGetValue((classHandle, fieldName), out var existing))
            {
                return existing;
            }

            if (_codeRegistration is null || !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var localIndex = _metadata.FindFieldLocalIndex(typeIndex, fieldName);
            if (localIndex < 0)
            {
                return 0;
            }

            var offset = _codeRegistration.GetFieldOffset(typeIndex, localIndex);
            if (offset < 0)
            {
                return 0;
            }

            var block = (byte*)NativeMemory.AllocZeroed(FieldInfoSize);
            *(nint*)(block + FieldInfo_Name) = AllocateCString(fieldName);
            *(nint*)(block + FieldInfo_Parent) = classHandle;
            *(int*)(block + FieldInfo_Offset) = offset;
            var handle = (nint)block;
            _fieldInfoByName[(classHandle, fieldName)] = handle;
            _ownedFieldInfos.Add(handle);
            return handle;
        }
    }

    /// <summary>Reads the runtime offset out of an Il2CppFieldInfo handle produced above, or -1.</summary>
    public unsafe int GetFieldOffset(nint fieldHandle)
    {
        lock (_gate)
        {
            if (fieldHandle == 0 || !_ownedFieldInfos.Contains(fieldHandle))
            {
                return -1;
            }

            return *(int*)((byte*)fieldHandle + FieldInfo_Offset);
        }
    }

    public nint GetMethodAtOrdinal(nint classHandle, int ordinal)
    {
        if (classHandle == 0 || ordinal < 0 || _metadata is null)
        {
            return 0;
        }

        lock (_gate)
        {
            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var count = _metadata.GetMethodCount(typeIndex);
            var start = _metadata.GetMethodStart(typeIndex);
            if (ordinal >= count || start < 0)
            {
                return 0;
            }

            return GetOrCreateMethod(start + ordinal);
        }
    }

    public int GetClassMethodCount(nint classHandle)
    {
        lock (_gate)
        {
            return _metadata is not null && _typeIndexByClass.TryGetValue(classHandle, out var typeIndex)
                ? _metadata.GetMethodCount(typeIndex)
                : 0;
        }
    }

    public uint GetClassFlags(nint classHandle)
    {
        lock (_gate)
        {
            return _metadata is not null && _typeIndexByClass.TryGetValue(classHandle, out var typeIndex)
                ? _metadata.GetTypeFlags(typeIndex)
                : 0;
        }
    }

    public nint GetMethodFromName(nint classHandle, string methodName, int argumentCount)
    {
        if (classHandle == 0 || string.IsNullOrEmpty(methodName) || _metadata is null)
        {
            return 0;
        }

        lock (_gate)
        {
            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var methodIndex = _metadata.FindMethodIndex(typeIndex, methodName, argumentCount);
            return methodIndex < 0 ? 0 : GetOrCreateMethod(methodIndex);
        }
    }

    public nint GetMethodName(nint methodHandle)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out _))
            {
                return 0;
            }

            unsafe
            {
                return *(nint*)((byte*)methodHandle + MethodInfo_Name);
            }
        }
    }

    public nint GetMethodClass(nint methodHandle)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out var methodIndex) || _metadata is null)
            {
                return 0;
            }

            var declaringType = _metadata.GetMethodDeclaringType(methodIndex);
            return declaringType < 0 ? 0 : GetOrCreateClass(declaringType);
        }
    }

    public uint GetMethodToken(nint methodHandle)
    {
        lock (_gate)
        {
            return TryGetMethodIndex(methodHandle, out var methodIndex) && _metadata is not null
                ? _metadata.GetMethodToken(methodIndex)
                : 0;
        }
    }

    public uint GetMethodFlags(nint methodHandle, out uint implementationFlags)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out var methodIndex) || _metadata is null)
            {
                implementationFlags = 0;
                return 0;
            }

            implementationFlags = _metadata.GetMethodImplementationFlags(methodIndex);
            return _metadata.GetMethodFlags(methodIndex);
        }
    }

    public uint GetMethodParameterCount(nint methodHandle)
    {
        lock (_gate)
        {
            return TryGetMethodIndex(methodHandle, out var methodIndex) && _metadata is not null
                ? (uint)_metadata.GetMethodParameterCount(methodIndex)
                : 0;
        }
    }

    public nint GetMethodPointer(nint methodHandle)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out _))
            {
                return 0;
            }

            unsafe
            {
                return *(nint*)((byte*)methodHandle + MethodInfo_MethodPointer);
            }
        }
    }

    /// <summary>Allocates a System.String object with the correct v24 layout and returns its address.</summary>
    public unsafe nint NewString(ReadOnlySpan<char> value)
    {
        var length = value.Length;
        // header (16) + int32 length (4) + (length+1) UTF-16 code units.
        var totalSize = (nuint)(ObjectHeaderSize + sizeof(int) + (length + 1) * sizeof(char));
        var block = (byte*)NativeMemory.AllocZeroed(totalSize);
        *(nint*)block = ResolveStringClass();
        *(int*)(block + ObjectHeaderSize) = length;
        var chars = (char*)(block + ObjectHeaderSize + sizeof(int));
        value.CopyTo(new Span<char>(chars, length));
        chars[length] = '\0';
        return (nint)block;
    }

    public unsafe int GetStringLength(nint stringHandle)
    {
        if (stringHandle == 0)
        {
            return 0;
        }

        return *(int*)((byte*)stringHandle + ObjectHeaderSize);
    }

    public nint GetStringChars(nint stringHandle)
    {
        if (stringHandle == 0)
        {
            return 0;
        }

        return stringHandle + ObjectHeaderSize + sizeof(int);
    }

    /// <summary>Returns a stable, dereferenceable array-class handle for an element/rank shape.</summary>
    public unsafe nint GetArrayClass(nint elementClass, uint rank, bool bounded = false)
    {
        if (elementClass == 0 || rank == 0 || rank > 32)
        {
            return 0;
        }

        lock (_gate)
        {
            var key = (elementClass, rank, bounded);
            if (_arrayClassByShape.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var block = (byte*)NativeMemory.AllocZeroed(ClassBlockSize);
            var elementNamePointer = GetClassName(elementClass);
            var elementName = elementNamePointer == 0
                ? "Object"
                : Marshal.PtrToStringUTF8(elementNamePointer) ?? "Object";
            var arrayNamePtr = AllocateCString(
                rank == 1 ? elementName + "[]" : elementName + "[" + new string(',', (int)rank - 1) + "]");
            var arrayNamespacePtr = GetClassNamespace(elementClass);
            *(nint*)(block + Class_Name) = arrayNamePtr;
            *(nint*)(block + Class_Namespace) = arrayNamespacePtr;

            var handle = (nint)block;
            // Register the name/namespace so the il2cpp_class_get_name/_namespace API path returns the
            // array's real name (e.g. "Int32[]") rather than falling through to the empty-name default.
            _classNamePtr[handle] = arrayNamePtr;
            _classNamespacePtr[handle] = arrayNamespacePtr;
            _arrayClassByShape[key] = handle;
            // Object/reference arrays are the only form currently exposed by the metadata HLE.
            _arrayElementSizeByClass[handle] = (nuint)IntPtr.Size;
            return handle;
        }
    }

    /// <summary>Allocates an IL2CPP array object with the standard 64-bit header.</summary>
    public unsafe nint NewArray(nint arrayClass, ulong length)
    {
        if (arrayClass == 0 || length > MaxArrayLength)
        {
            return 0;
        }

        lock (_gate)
        {
            var elementSize = _arrayElementSizeByClass.TryGetValue(arrayClass, out var knownSize)
                ? knownSize
                : (nuint)IntPtr.Size;
            if (length > ((ulong)nuint.MaxValue - ArrayHeaderSize) / (ulong)elementSize)
            {
                return 0;
            }

            var byteLength = checked(length * (ulong)elementSize);
            var totalSize = checked((nuint)(ArrayHeaderSize + byteLength));
            var block = (byte*)NativeMemory.AllocZeroed(totalSize);
            *(nint*)block = arrayClass;
            *(ulong*)(block + ArrayLengthOffset) = length;
            var handle = (nint)block;
            _ownedArrays[handle] = (length, byteLength);
            return handle;
        }
    }

    public ulong GetArrayLength(nint arrayHandle)
    {
        lock (_gate)
        {
            return _ownedArrays.TryGetValue(arrayHandle, out var info) ? info.Length : 0;
        }
    }

    public ulong GetArrayByteLength(nint arrayHandle)
    {
        lock (_gate)
        {
            return _ownedArrays.TryGetValue(arrayHandle, out var info) ? info.ByteLength : 0;
        }
    }

    public nuint GetArrayElementSize(nint arrayClass)
    {
        lock (_gate)
        {
            return _arrayElementSizeByClass.TryGetValue(arrayClass, out var size)
                ? size
                : (nuint)IntPtr.Size;
        }
    }

    /// <summary>Allocates a zeroed object using the class's registered runtime instance size.</summary>
    public unsafe nint NewObject(nint classHandle)
    {
        if (classHandle == 0)
        {
            return 0;
        }

        var size = Math.Max((ulong)GetInstanceSize(classHandle), (ulong)ObjectHeaderSize);
        var block = (byte*)NativeMemory.AllocZeroed((nuint)size);
        *(nint*)block = classHandle;
        return (nint)block;
    }

    private nint ResolveStringClass()
    {
        if (_stringClassResolved)
        {
            return _stringClass;
        }

        lock (_gate)
        {
            if (!_stringClassResolved)
            {
                _stringClass = GetClassFromName("System", "String");
                _stringClassResolved = true;
            }
        }

        return _stringClass;
    }

    private nint GetOrCreateClass(int typeIndex)
    {
        lock (_gate)
        {
            if (_classByTypeIndex.TryGetValue(typeIndex, out var existing))
            {
                return existing;
            }

            var handle = AllocateClassBlock(typeIndex);
            _classByTypeIndex[typeIndex] = handle;
            _typeIndexByClass[handle] = typeIndex;
            return handle;
        }
    }

    private unsafe nint GetOrCreateMethod(int methodIndex)
    {
        if (_metadata is null || (uint)methodIndex >= (uint)_metadata.MethodCount)
        {
            return 0;
        }

        if (_methodByIndex.TryGetValue(methodIndex, out var existing))
        {
            return existing;
        }

        var block = (byte*)NativeMemory.AllocZeroed(MethodInfoSize);
        *(nint*)(block + MethodInfo_Name) = AllocateCString(_metadata.GetMethodName(methodIndex));
        *(nint*)(block + MethodInfo_Class) = GetOrCreateClass(_metadata.GetMethodDeclaringType(methodIndex));
        *(uint*)(block + MethodInfo_Token) = _metadata.GetMethodToken(methodIndex);
        *(ushort*)(block + MethodInfo_Flags) = _metadata.GetMethodFlags(methodIndex);
        *(ushort*)(block + MethodInfo_IfFlags) = _metadata.GetMethodImplementationFlags(methodIndex);
        *(ushort*)(block + MethodInfo_Slot) = _metadata.GetMethodSlot(methodIndex);
        *(ushort*)(block + MethodInfo_ParameterCount) = _metadata.GetMethodParameterCount(methodIndex);

        var handle = (nint)block;
        _methodByIndex[methodIndex] = handle;
        _methodIndexByHandle[handle] = methodIndex;
        return handle;
    }

    private bool TryGetMethodIndex(nint methodHandle, out int methodIndex)
    {
        if (methodHandle != 0 && _methodIndexByHandle.TryGetValue(methodHandle, out methodIndex))
        {
            return true;
        }

        methodIndex = -1;
        return false;
    }

    private unsafe nint AllocateClassBlock(int typeIndex)
    {
        var block = (byte*)NativeMemory.AllocZeroed(ClassBlockSize);
        if (_metadata is not null)
        {
            // Populate the two inline-read offsets; everything else stays zeroed (safe default).
            *(nint*)(block + Class_Name) = AllocateCString(_metadata.GetTypeName(typeIndex));
            *(nint*)(block + Class_Namespace) = AllocateCString(_metadata.GetTypeNamespace(typeIndex));
        }

        return (nint)block;
    }

    private static unsafe nint AllocateCString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var block = (byte*)NativeMemory.AllocZeroed((nuint)(bytes.Length + 1));
        bytes.CopyTo(new Span<byte>(block, bytes.Length));
        return (nint)block;
    }

    private static string? ResolveMetadataPath()
    {
        var app0 = Environment.GetEnvironmentVariable("SHARPEMU_APP0_DIR");
        if (string.IsNullOrWhiteSpace(app0))
        {
            return null;
        }

        return Path.Combine(app0, "Media", "Metadata", "global-metadata.dat");
    }
}
