// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Runtime.InteropServices;
using System.Threading;

using SharpEmu.Logging;

namespace SharpEmu.Libs.Kernel;

public static class KernelExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Kernel");

    private static readonly object _cxaGate = new();
    private static readonly List<CxaDestructorEntry> _cxaDestructors = new();
    private static readonly object _coredumpGate = new();
    private static readonly object _environmentGate = new();
    private static readonly Dictionary<string, nint> _environmentStrings = new(StringComparer.Ordinal);
    private static ulong _coredumpHandler;
    private static ulong _coredumpHandlerContext;
    private static int _randState = 1;

    private readonly record struct CxaDestructorEntry(
        ulong Function,
        ulong Argument,
        ulong ModuleHandle);

    [SysAbiExport(
        Nid = "WB66evu8bsU",
        ExportName = "sceKernelGetCompiledSdkVersion",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetCompiledSdkVersion(CpuContext ctx)
    {
        _ = ctx;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "8zLSfEfW5AU",
        ExportName = "sceCoredumpRegisterCoredumpHandler",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceCoredump")]
    public static int CoredumpRegisterHandler(CpuContext ctx)
    {
        lock (_coredumpGate)
        {
            _coredumpHandler = ctx[CpuRegister.Rdi];
            _coredumpHandlerContext = ctx[CpuRegister.Rsi];
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "uMei1W9uyNo",
        ExportName = "exit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Exit(CpuContext ctx)
    {
        var status = unchecked((int)ctx[CpuRegister.Rdi]);
        Log.Info($"exit(status={status})");
        GuestThreadExecution.RequestCurrentEntryExit("exit", status);
        ctx[CpuRegister.Rax] = unchecked((ulong)status);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "XKRegsFpEpk",
        ExportName = "catchReturnFromMain",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int CatchReturnFromMain(CpuContext ctx)
    {
        var status = unchecked((int)ctx[CpuRegister.Rdi]);
        Log.Info($"catchReturnFromMain(status={status})");
        GuestThreadExecution.RequestCurrentEntryExit("catchReturnFromMain", status);
        ctx[CpuRegister.Rax] = unchecked((ulong)status);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "bzQExy189ZI",
        ExportName = "_init_env",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int InitEnv(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "8G2LB+A3rzg",
        ExportName = "atexit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Atexit(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "tsvEmnenz48",
        ExportName = "__cxa_atexit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int CxaAtexit(CpuContext ctx)
    {
        var destructorFunction = ctx[CpuRegister.Rdi];
        var destructorArgument = ctx[CpuRegister.Rsi];
        var moduleHandle = ctx[CpuRegister.Rdx];
        if (destructorFunction == 0)
        {
            ctx[CpuRegister.Rax] = 0;
            return (int)OrbisGen2Result.ORBIS_GEN2_OK;
        }

        lock (_cxaGate)
        {
            _cxaDestructors.Add(new CxaDestructorEntry(
                destructorFunction,
                destructorArgument,
                moduleHandle));
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "H2e8t5ScQGc",
        ExportName = "__cxa_finalize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int CxaFinalize(CpuContext ctx)
    {
        var moduleHandle = ctx[CpuRegister.Rdi];

        lock (_cxaGate)
        {
            if (moduleHandle == 0)
            {
                _cxaDestructors.Clear();
            }
            else
            {
                for (var i = _cxaDestructors.Count - 1; i >= 0; i--)
                {
                    if (_cxaDestructors[i].ModuleHandle == moduleHandle)
                    {
                        _cxaDestructors.RemoveAt(i);
                    }
                }
            }
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "kbw4UHHSYy0",
        ExportName = "__pthread_cxa_finalize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadCxaFinalize(CpuContext ctx)
    {
        _ = ctx;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "6Z83sYWFlA8",
        ExportName = "_exit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int UnderscoreExit(CpuContext ctx)
    {
        _ = ctx;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "Ac86z8q7T8A",
        ExportName = "sceKernelExitSblock",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelExitSblock(CpuContext ctx)
    {
        _ = ctx;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "6UgtwV+0zb4",
        ExportName = "scePthreadCreate",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadCreate(CpuContext ctx)
    {
        var threadIdAddress = ctx[CpuRegister.Rdi];
        var attrAddress = ctx[CpuRegister.Rsi];
        var entryAddress = ctx[CpuRegister.Rdx];
        var argument = ctx[CpuRegister.Rcx];
        var nameAddress = ctx[CpuRegister.R8];
        var name = nameAddress == 0 ? string.Empty : ReadCString(ctx, nameAddress, 256);
        var threadHandle = KernelPthreadState.CreateThreadHandle(name);
        KernelPthreadExtendedCompatExports.GetThreadStartScheduling(
            ctx,
            attrAddress,
            out var priority,
            out var affinityMask);
        KernelPthreadExtendedCompatExports.RegisterThreadStart(
            threadHandle,
            name,
            priority,
            affinityMask);
        if (threadIdAddress != 0 &&
            !KernelMemoryCompatExports.TryWriteUInt64Compat(ctx, threadIdAddress, threadHandle))
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
        }

        if (ShouldTracePthread())
        {
            Log.Trace(
                $"pthread_create: out=0x{threadIdAddress:X16} attr=0x{attrAddress:X16} " +
                $"entry=0x{entryAddress:X16} arg=0x{argument:X16} name_ptr=0x{nameAddress:X16} " +
                $"name='{name}' priority={priority} affinity=0x{affinityMask:X} -> thread=0x{threadHandle:X16}");
        }

        var scheduler = GuestThreadExecution.Scheduler;
        var emulateSilently = string.Equals(name, "SDLAudioP2", StringComparison.Ordinal);
        if (emulateSilently)
        {
            Log.Info("pthread_create: emulating SDL audio callback thread with the silent audio backend.");
        }
        else if (scheduler is not null && entryAddress != 0)
        {
            var request = new GuestThreadStartRequest(
                threadHandle,
                entryAddress,
                argument,
                attrAddress,
                name,
                priority,
                affinityMask);
            if (!scheduler.TryStartThread(ctx, request, out var error))
            {
                Log.Error(
                    $"pthread_create: failed to schedule guest thread '{name}' entry=0x{entryAddress:X16}: {error}");
                ctx[CpuRegister.Rax] = unchecked((ulong)(int)OrbisGen2Result.ORBIS_GEN2_ERROR_TRY_AGAIN);
                return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_TRY_AGAIN;
            }
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "OxhIB8LB-PQ",
        ExportName = "pthread_create",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadCreate(CpuContext ctx)
    {
        return PthreadCreate(ctx);
    }

    [SysAbiExport(
        Nid = "__hle_attr_detach",
        ExportName = "pthread_attr_setdetachstate",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadAttrSetDetachState(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_setjmp",
        ExportName = "setjmp",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Setjmp(CpuContext ctx)
    {
        // Valid libpng inputs only need the initial setjmp return. longjmp state
        // remains unnecessary until guest-side decoder errors are propagated.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "Jmi+9w9u0E4",
        ExportName = "pthread_create_name_np",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadCreateNameNp(CpuContext ctx)
    {
        return PthreadCreate(ctx);
    }

    [SysAbiExport(
        Nid = "3kg7rT0NQIs",
        ExportName = "scePthreadExit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadExit(CpuContext ctx)
    {
        var value = ctx[CpuRegister.Rdi];
        GuestThreadExecution.RequestCurrentEntryExit("scePthreadExit", value);
        ctx[CpuRegister.Rax] = value;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "FJrT5LuUBAU",
        ExportName = "pthread_exit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePosix")]
    public static int PosixPthreadExit(CpuContext ctx)
    {
        var value = ctx[CpuRegister.Rdi];
        GuestThreadExecution.RequestCurrentEntryExit("pthread_exit", value);
        ctx[CpuRegister.Rax] = value;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "onNY9Byn-W8",
        ExportName = "scePthreadJoin",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadJoin(CpuContext ctx)
    {
        var threadId = ctx[CpuRegister.Rdi];
        var returnValueAddress = ctx[CpuRegister.Rsi];
        if (GuestThreadExecution.Scheduler is { } scheduler &&
            !scheduler.TryJoinThread(threadId))
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_TRY_AGAIN;
        }

        if (returnValueAddress != 0 &&
            !KernelMemoryCompatExports.TryWriteUInt64Compat(ctx, returnValueAddress, 0))
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
        }

        if (ShouldTracePthread())
        {
            Log.Trace(
                $"pthread_join: thread=0x{threadId:X16} retval_out=0x{returnValueAddress:X16}");
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "h9CcP3J0oVM",
        ExportName = "pthread_join",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadJoin(CpuContext ctx)
    {
        return PthreadJoin(ctx);
    }

    [SysAbiExport(
        Nid = "wuCroIGjt2g",
        ExportName = "open",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Open(CpuContext ctx) => KernelMemoryCompatExports.KernelOpenUnderscore(ctx);

    [SysAbiExport(
        Nid = "1G3lF1Gg1k8",
        ExportName = "sceKernelOpen",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelOpen(CpuContext ctx) => KernelMemoryCompatExports.KernelOpenUnderscore(ctx);

    [SysAbiExport(
        Nid = "hcuQgD53UxM",
        ExportName = "printf",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Printf(CpuContext ctx)
    {
        ulong fmtPtr = ctx[CpuRegister.Rdi];
        string fmt = ReadCString(ctx, fmtPtr, 4096);
        string outStr = KernelMemoryCompatExports.FormatStringFromVarArgs(ctx, fmt, firstGpArgIndex: 1);
        if (outStr.EndsWith('\n') || outStr.EndsWith('\r'))
        {
            Console.Write($"[DEBUG][PRINF] {outStr}");
        }
        else
        {
            Console.WriteLine($"[DEBUG][PRINF] {outStr}");
        }

        ctx[CpuRegister.Rax] = (ulong)System.Text.Encoding.UTF8.GetByteCount(outStr);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "EMutwaQ34Jo",
        ExportName = "perror",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Perror(CpuContext ctx)
    {
        ulong sPtr = ctx[CpuRegister.Rdi];

        string msg;
        if (sPtr == 0)
        {
            msg = "perror(NULL)";
        }
        else
        {
            msg = ReadCString(ctx, sPtr, 2048);
            msg = $"perror(\"{msg}\")";
        }

        Console.WriteLine(msg);

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_getenv",
        ExportName = "getenv",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Getenv(CpuContext ctx)
    {
        var name = ReadCString(ctx, ctx[CpuRegister.Rdi], 1024);
        if (!name.StartsWith("SDL_", StringComparison.Ordinal))
        {
            ctx[CpuRegister.Rax] = 0;
            return (int)OrbisGen2Result.ORBIS_GEN2_OK;
        }

        var value = Environment.GetEnvironmentVariable(name);
        if (value is null)
        {
            ctx[CpuRegister.Rax] = 0;
            return (int)OrbisGen2Result.ORBIS_GEN2_OK;
        }

        var cacheKey = $"{name}={value}";
        lock (_environmentGate)
        {
            if (!_environmentStrings.TryGetValue(cacheKey, out var pointer))
            {
                pointer = Marshal.StringToHGlobalAnsi(value);
                _environmentStrings[cacheKey] = pointer;
            }

            ctx[CpuRegister.Rax] = unchecked((ulong)pointer);
        }

        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_puts",
        ExportName = "puts",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Puts(CpuContext ctx)
    {
        var text = ReadCString(ctx, ctx[CpuRegister.Rdi], 4096);
        Console.WriteLine(text);
        ctx[CpuRegister.Rax] = unchecked((ulong)text.Length + 1);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_time",
        ExportName = "time",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Time(CpuContext ctx)
    {
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var outputAddress = ctx[CpuRegister.Rdi];
        if (outputAddress != 0 && !ctx.TryWriteUInt64(outputAddress, unchecked((ulong)seconds)))
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
        }

        ctx[CpuRegister.Rax] = unchecked((ulong)seconds);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_srand",
        ExportName = "srand",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Srand(CpuContext ctx)
    {
        Volatile.Write(ref _randState, unchecked((int)ctx[CpuRegister.Rdi]));
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_rand",
        ExportName = "rand",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libc")]
    public static int Rand(CpuContext ctx)
    {
        int initial;
        int updated;
        do
        {
            initial = Volatile.Read(ref _randState);
            updated = unchecked((initial * 1103515245) + 12345);
        }
        while (Interlocked.CompareExchange(ref _randState, updated, initial) != initial);

        ctx[CpuRegister.Rax] = unchecked((uint)(updated >> 16) & 0x7FFFu);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__hle_pthread_once",
        ExportName = "pthread_once",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadOnce(CpuContext ctx)
    {
        return KernelPthreadCompatExports.PthreadOnce(ctx);
    }

    private static string ReadCString(CpuContext ctx, ulong address, int maxLen)
    {
        Span<byte> buf = stackalloc byte[maxLen];
        if (!ctx.Memory.TryRead(address, buf))
            return $"<unreadable 0x{address:X16}>";

        int len = 0;
        while (len < buf.Length && buf[len] != 0) len++;

        try { return System.Text.Encoding.UTF8.GetString(buf.Slice(0, len)); }
        catch { return System.Text.Encoding.ASCII.GetString(buf.Slice(0, len)); }
    }

    private static bool ShouldTracePthread()
    {
        return string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_LOG_PTHREADS"), "1", StringComparison.Ordinal);
    }
}
