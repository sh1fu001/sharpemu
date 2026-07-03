// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Threading;
using SharpEmu.HLE;

namespace SharpEmu.Libs.CommonDialog;

public static class CommonDialogExports
{
    private const int AlreadySystemInitialized = unchecked((int)0x80B80002);
    private static int _initialized;

    [SysAbiExport(
        Nid = "uoUpLGNkygk",
        ExportName = "sceCommonDialogInitialize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceCommonDialog")]
    public static int CommonDialogInitialize(CpuContext ctx)
    {
        var result = Interlocked.Exchange(ref _initialized, 1) == 0
            ? 0
            : AlreadySystemInitialized;
        ctx[CpuRegister.Rax] = unchecked((ulong)result);
        return result;
    }

    [SysAbiExport(
        Nid = "BQ3tey0JmQM",
        ExportName = "sceCommonDialogIsUsed",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceCommonDialog")]
    public static int CommonDialogIsUsed(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // sceCommonDialogStatus: 0=NONE, 1=INITIALIZED, 2=RUNNING, 3=FINISHED.
    private const int MsgDialogStatusNone = 0;
    private const int MsgDialogStatusInitialized = 1;
    private const int MsgDialogStatusFinished = 3;
    private static int _msgDialogStatus = MsgDialogStatusNone;

    [SysAbiExport(
        Nid = "lDqxaY1UbEo",
        ExportName = "sceMsgDialogInitialize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogInitialize(CpuContext ctx)
    {
        Interlocked.Exchange(ref _msgDialogStatus, MsgDialogStatusInitialized);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "b06Hh0DPEaE",
        ExportName = "sceMsgDialogOpen",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogOpen(CpuContext ctx)
    {
        // With no interactive UI, immediately mark the dialog finished so the caller's
        // updateStatus/getStatus poll loop terminates instead of spinning forever.
        Interlocked.Exchange(ref _msgDialogStatus, MsgDialogStatusFinished);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "6fIC3XKt2k0",
        ExportName = "sceMsgDialogUpdateStatus",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogUpdateStatus(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Volatile.Read(ref _msgDialogStatus));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "CWVW78Qc3fI",
        ExportName = "sceMsgDialogGetStatus",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogGetStatus(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Volatile.Read(ref _msgDialogStatus));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "Lr8ovHH9l6A",
        ExportName = "sceMsgDialogGetResult",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogGetResult(CpuContext ctx)
    {
        // OrbisMsgDialogResult { int mode; int result; int buttonId; ... }. Report result=0 (OK)
        // so the caller proceeds down its success path.
        var resultAddress = ctx[CpuRegister.Rdi];
        if (resultAddress != 0)
        {
            Span<byte> result = stackalloc byte[0x20];
            result.Clear();
            if (!ctx.Memory.TryWrite(resultAddress, result))
            {
                return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
            }
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "HTrcDKlFKuM",
        ExportName = "sceMsgDialogClose",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogClose(CpuContext ctx)
    {
        Interlocked.Exchange(ref _msgDialogStatus, MsgDialogStatusFinished);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "ePw-kqZmelo",
        ExportName = "sceMsgDialogTerminate",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogTerminate(CpuContext ctx)
    {
        Interlocked.Exchange(ref _msgDialogStatus, MsgDialogStatusNone);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "wTpfglkmv34",
        ExportName = "sceMsgDialogProgressBarSetValue",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceMsgDialog")]
    public static int MsgDialogProgressBarSetValue(CpuContext ctx)
    {
        // No visible progress bar; accept the update so a progress dialog's driver loop advances.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }
}
