// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Core.Cpu;

/// <summary>
/// Real Sony-hashed NIDs for Unity/IL2CPP entry points that need bespoke dispatch in
/// <see cref="Native.DirectExecutionBackend"/> instead of a plain <c>SysAbiExport</c>, because
/// handling them requires creating native trampolines at runtime (see
/// <c>DirectExecutionBackend.Imports.DispatchIl2CppApiLookupSymbol</c>).
/// </summary>
internal static class Il2CppNids
{
    /// <summary>il2cpp_api_lookup_symbol(const char* name) -&gt; void*</summary>
    public const string ApiLookupSymbol = "r8mvOaWdi28";
}
