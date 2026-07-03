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

    // Well-established version-24 Il2CppClass field offsets used for inline reads by generated code.
    private const int Class_Name = 0x10;
    private const int Class_Namespace = 0x18;
    private const int ClassBlockSize = 0x160; // over-allocated + zeroed; unknown fields read as 0.

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");
    private static readonly Lazy<Il2CppRuntime> _instance = new(() => new Il2CppRuntime());

    public static Il2CppRuntime Instance => _instance.Value;

    private readonly object _gate = new();
    private readonly Dictionary<string, nint> _internalCalls = new(StringComparer.Ordinal);
    private readonly Il2CppMetadata? _metadata;
    private readonly Dictionary<int, nint> _classByTypeIndex = new();
    private readonly Dictionary<nint, int> _typeIndexByClass = new();
    private readonly Dictionary<nint, nint> _classNamePtr = new();
    private readonly Dictionary<nint, nint> _classNamespacePtr = new();

    private nint _stringClass;
    private bool _stringClassResolved;

    public bool MetadataAvailable => _metadata is not null;

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
                return 0;
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
                return 0;
            }

            nsPtr = AllocateCString(_metadata.GetTypeNamespace(typeIndex));
            _classNamespacePtr[classHandle] = nsPtr;
            return nsPtr;
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
