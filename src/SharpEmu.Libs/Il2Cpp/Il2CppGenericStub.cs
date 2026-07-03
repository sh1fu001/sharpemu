// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using SharpEmu.HLE;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Fallback body installed by <c>DirectExecutionBackend.TryResolveOrCreateIl2CppSymbol</c> for any
/// il2cpp_* symbol name requested through il2cpp_api_lookup_symbol that has no explicit HLE
/// implementation in <see cref="Il2CppRuntimeExports"/>. It exists so the guest always gets a valid,
/// callable function pointer back (never a null/unresolved sentinel that would fault), at the cost of
/// the call being a no-op that returns zero. This intentionally does not attempt to emulate real
/// IL2CPP semantics (class metadata, reflection, method invocation) - see docs/game-notes/PPSA09804.md.
/// </summary>
public static class Il2CppGenericStub
{
    private const int MaxLoggedCallsPerSymbol = 8;

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");
    private static readonly ConcurrentDictionary<string, int> _callCounts = new(StringComparer.Ordinal);

    public static int HandleUnimplementedSymbol(CpuContext ctx, string symbolName)
    {
        var count = _callCounts.AddOrUpdate(symbolName, 1, static (_, existing) => existing + 1);
        if (count <= MaxLoggedCallsPerSymbol)
        {
            Log.Warning(
                $"il2cpp symbol '{symbolName}' has no HLE implementation (call #{count}); " +
                $"returning 0 from a generic stub. rdi=0x{ctx[CpuRegister.Rdi]:X16} rsi=0x{ctx[CpuRegister.Rsi]:X16}");
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }
}
