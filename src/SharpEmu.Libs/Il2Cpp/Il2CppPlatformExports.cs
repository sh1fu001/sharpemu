// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Handlers for the small set of Unity/IL2CPP platform entry points that arrive as real,
/// statically-resolved NIDs in the SELF import table - as opposed to the ~200 il2cpp_* embedding
/// API functions that are resolved dynamically through il2cpp_api_lookup_symbol
/// (see <see cref="Il2CppRuntimeExports"/> and <c>DirectExecutionBackend.Imports.cs</c>).
/// </summary>
public static class Il2CppPlatformExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");
    private static string? _dataFolder;

    [SysAbiExport(
        Nid = "cJ2Y4E-t258",
        ExportName = "il2cpp_api_register_symbols",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libIl2Cpp")]
    public static int ApiRegisterSymbols(CpuContext ctx)
    {
        // The guest hands us its own name->pointer table here, but symbol resolution in this
        // emulator flows the other way (il2cpp_api_lookup_symbol asks the host for pointers), so
        // there is nothing to consume yet. Acknowledge success so IL2CPP init keeps advancing.
        Log.Info(
            $"il2cpp_api_register_symbols(table=0x{ctx[CpuRegister.Rdi]:X16}, count=0x{ctx[CpuRegister.Rsi]:X16}) acknowledged.");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "35NoyMOtYpE",
        ExportName = "SetDataFolder",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libIl2Cpp")]
    public static int SetDataFolder(CpuContext ctx)
    {
        var pathAddress = ctx[CpuRegister.Rdi];
        _dataFolder = Il2CppStrings.TryReadAscii(ctx, pathAddress, 512, out var path) ? path : null;
        Log.Info($"SetDataFolder('{_dataFolder ?? "<unreadable>"}')");
        ctx[CpuRegister.Rax] = 1;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "-pnj3-7a6QA",
        ExportName = "unity_mono_set_user_malloc_mutex",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libIl2Cpp")]
    public static int UnityMonoSetUserMallocMutex(CpuContext ctx)
    {
        // The allocator behind il2cpp_alloc/il2cpp_free (Il2CppGuestHeap, host fallback) is already
        // thread-safe, so there is no user-provided mutex to install.
        Log.Trace($"unity_mono_set_user_malloc_mutex(mutex=0x{ctx[CpuRegister.Rdi]:X16}) ignored.");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }
}
