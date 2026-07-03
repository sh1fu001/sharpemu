// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;

namespace SharpEmu.Libs.Audio;

/// <summary>
/// Minimal HLE for the AJM (Audio Job Manager) decode-scheduling library. No real hardware decoder
/// is emulated yet; these lifecycle entries exist so titles that bring AJM up during startup keep
/// advancing rather than faulting on an unresolved import. Codec batch/decode entries are
/// intentionally left unimplemented (they need a real decoder), so audio simply stays silent.
/// </summary>
public static class AjmExports
{
    [SysAbiExport(Nid = "d4SQL+QQLTY", ExportName = "sceAjmInitialize", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmInitialize(CpuContext ctx)
    {
        // int sceAjmInitialize(int64_t reserved, SceAjmContextId* outContext). Hand back a fixed
        // non-zero context id so callers that stash and re-use it stay consistent.
        var outContextAddress = ctx[CpuRegister.Rsi];
        if (outContextAddress != 0)
        {
            Span<byte> contextId = stackalloc byte[sizeof(uint)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(contextId, 1);
            if (!ctx.Memory.TryWrite(outContextAddress, contextId))
            {
                return Fail(ctx);
            }
        }

        return Ok(ctx);
    }

    [SysAbiExport(Nid = "Ct3WeO240lw", ExportName = "sceAjmFinalize", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmFinalize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "Wi7DtlLV+KI", ExportName = "sceAjmModuleRegister", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmModuleRegister(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "AxhcqVv5AYU", ExportName = "sceAjmModuleUnregister", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmModuleUnregister(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "pIpGiaYkHkM", ExportName = "sceAjmMemoryRegister", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmMemoryRegister(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "Q3dyFuwGn64", ExportName = "sceAjmMemoryUnregister", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmMemoryUnregister(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "dl+4eHSzUu4", ExportName = "sceAjmGetFailedInstance", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmGetFailedInstance(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "diXjQNiMu-s", ExportName = "sceAjmInstanceAvailable", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmInstanceAvailable(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "XX3Kr-nBf7E", ExportName = "sceAjmStrError", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAjm")]
    public static int AjmStrError(CpuContext ctx)
    {
        // const char* sceAjmStrError(int). Point the caller at an empty string constant.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static int Ok(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static int Fail(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)(int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
    }
}
