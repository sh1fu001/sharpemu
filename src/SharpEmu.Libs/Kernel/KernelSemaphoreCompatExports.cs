// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using SharpEmu.HLE;

using SharpEmu.Logging;

namespace SharpEmu.Libs.Kernel;

public static class KernelSemaphoreCompatExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Kernel");

    private const int MaxSemaphoreNameLength = 128;
    private static readonly ConcurrentDictionary<uint, KernelSemaphoreState> _semaphores = new();
    private static readonly ConcurrentDictionary<ulong, uint> _handlesByStorageAddress = new();
    private static int _nextSemaphoreHandle = 1;

    private sealed class KernelSemaphoreState
    {
        public required string Name { get; init; }
        public required int InitialCount { get; init; }
        public required int MaxCount { get; init; }
        public int Count { get; set; }
        public int WaitingThreads { get; set; }
        public bool IsDeleted { get; set; }
        public object Gate { get; } = new();
    }

    [SysAbiExport(
        Nid = "188x57JYp0g",
        ExportName = "sceKernelCreateSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelCreateSema(CpuContext ctx)
    {
        var semaphoreAddress = ctx[CpuRegister.Rdi];
        var nameAddress = ctx[CpuRegister.Rsi];
        var attr = unchecked((uint)ctx[CpuRegister.Rdx]);
        var initialCount = unchecked((int)ctx[CpuRegister.Rcx]);
        var maxCount = unchecked((int)ctx[CpuRegister.R8]);
        var optionAddress = ctx[CpuRegister.R9];

        if (semaphoreAddress == 0 ||
            nameAddress == 0 ||
            attr > 2 ||
            initialCount < 0 ||
            maxCount <= 0 ||
            initialCount > maxCount ||
            optionAddress != 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        if (!TryReadNullTerminatedUtf8(ctx, nameAddress, MaxSemaphoreNameLength, out var name))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        var handle = unchecked((uint)Interlocked.Increment(ref _nextSemaphoreHandle));
        if (handle == 0)
        {
            handle = unchecked((uint)Interlocked.Increment(ref _nextSemaphoreHandle));
        }

        _semaphores[handle] = new KernelSemaphoreState
        {
            Name = name,
            InitialCount = initialCount,
            MaxCount = maxCount,
            Count = initialCount,
        };

        if (!TryWriteUInt32(ctx, semaphoreAddress, handle))
        {
            _semaphores.TryRemove(handle, out _);
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        _handlesByStorageAddress[semaphoreAddress] = handle;
        TraceSemaphore($"create handle=0x{handle:X8} storage=0x{semaphoreAddress:X16} name='{name}' attr=0x{attr:X} init={initialCount} max={maxCount}");
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "Zxa0VhQVTsk",
        ExportName = "sceKernelWaitSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelWaitSema(CpuContext ctx)
    {
        var rawHandle = ctx[CpuRegister.Rdi];
        var needCount = unchecked((int)ctx[CpuRegister.Rsi]);
        var timeoutAddress = ctx[CpuRegister.Rdx];

        if (!TryResolveSemaphore(ctx, rawHandle, out var handle, out var semaphore))
        {
            if (rawHandle > uint.MaxValue &&
                timeoutAddress == 0 &&
                HasFmodSemaphoreTombstone())
            {
                TraceSemaphore(
                    $"wait-stale-fmod-wrapper raw=0x{rawHandle:X16}; treating teardown race as satisfied");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
            }

            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        if (needCount < 1 || needCount > semaphore.MaxCount)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        lock (semaphore.Gate)
        {
            if (semaphore.IsDeleted)
            {
                TraceSemaphore($"wait-deleted handle=0x{handle:X8} name='{semaphore.Name}'");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
            }

            if (semaphore.Count >= needCount)
            {
                semaphore.Count -= needCount;
                TraceSemaphore($"wait handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count}");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
            }

            if (timeoutAddress != 0)
            {
                if (!TryReadUInt32(ctx, timeoutAddress, out _))
                {
                    return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
                }

                _ = TryWriteUInt32(ctx, timeoutAddress, 0);
                TraceSemaphore($"wait-timeout handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count}");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_TIMED_OUT);
            }

            var blockedWaitResult = OrbisGen2Result.ORBIS_GEN2_OK;
            if (!GuestThreadExecution.RequestCurrentThreadBlock(
                    ctx,
                    "sceKernelWaitSema",
                    GetSemaphoreWakeKey(handle),
                    () => (int)blockedWaitResult,
                    () =>
                    {
                        lock (semaphore.Gate)
                        {
                            if (semaphore.IsDeleted)
                            {
                                semaphore.WaitingThreads = Math.Max(0, semaphore.WaitingThreads - 1);
                                TraceSemaphore(
                                    $"wait-delete-wake handle=0x{handle:X8} name='{semaphore.Name}' waiters={semaphore.WaitingThreads}");
                                return true;
                            }

                            if (semaphore.Count < needCount)
                            {
                                return false;
                            }

                            semaphore.Count -= needCount;
                            semaphore.WaitingThreads = Math.Max(0, semaphore.WaitingThreads - 1);
                            blockedWaitResult = OrbisGen2Result.ORBIS_GEN2_OK;
                            TraceSemaphore(
                                $"wait-wake handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count} waiters={semaphore.WaitingThreads}");
                            return true;
                        }
                    }))
            {
                if (string.Equals(semaphore.Name, "FMOD Semaphore", StringComparison.Ordinal))
                {
                    TraceSemaphore(
                        $"wait-fmod-cooperative-bypass handle=0x{handle:X8} need={needCount} count={semaphore.Count}");
                    return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
                }

                TraceSemaphore($"wait-would-block handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count}");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_TRY_AGAIN);
            }

            semaphore.WaitingThreads++;
            TraceSemaphore($"wait-block handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count} waiters={semaphore.WaitingThreads}");
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
        }
    }

    [SysAbiExport(
        Nid = "12wOHk8ywb0",
        ExportName = "sceKernelPollSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelPollSema(CpuContext ctx)
    {
        var rawHandle = ctx[CpuRegister.Rdi];
        var needCount = unchecked((int)ctx[CpuRegister.Rsi]);

        if (!TryResolveSemaphore(ctx, rawHandle, out var handle, out var semaphore))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        if (needCount < 1 || needCount > semaphore.MaxCount)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        lock (semaphore.Gate)
        {
            if (semaphore.Count < needCount)
            {
                TraceSemaphore($"poll-busy handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count}");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_BUSY);
            }

            semaphore.Count -= needCount;
            TraceSemaphore($"poll handle=0x{handle:X8} name='{semaphore.Name}' need={needCount} count={semaphore.Count}");
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
        }
    }

    [SysAbiExport(
        Nid = "4czppHBiriw",
        ExportName = "sceKernelSignalSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSignalSema(CpuContext ctx)
    {
        var rawHandle = ctx[CpuRegister.Rdi];
        var signalCount = unchecked((int)ctx[CpuRegister.Rsi]);

        if (!TryResolveSemaphore(ctx, rawHandle, out var handle, out var semaphore))
        {
            if (rawHandle == 0 && HasFmodSemaphoreTombstone())
            {
                TraceSemaphore(
                    "signal-stale-fmod-handle raw=0x0000000000000000; treating teardown race as a no-op");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
            }

            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        if (signalCount <= 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        lock (semaphore.Gate)
        {
            if (semaphore.IsDeleted)
            {
                TraceSemaphore(
                    $"signal-deleted handle=0x{handle:X8} name='{semaphore.Name}' signal={signalCount}");
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
            }

            if (semaphore.Count > semaphore.MaxCount - signalCount)
            {
                // FMOD can race its teardown or run a producer before its cooperatively scheduled
                // consumer. The native semaphore remains bounded, but this condition is not fatal
                // to FMOD; accepting the redundant signal avoids its tight error-retry loop.
                if (string.Equals(semaphore.Name, "FMOD Semaphore", StringComparison.Ordinal))
                {
                    TraceSemaphore(
                        $"signal-fmod-saturated handle=0x{handle:X8} signal={signalCount} count={semaphore.Count}");
                    return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
                }

                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
            }

            semaphore.Count += signalCount;
            TraceSemaphore($"signal handle=0x{handle:X8} name='{semaphore.Name}' signal={signalCount} count={semaphore.Count} waiters={semaphore.WaitingThreads}");
        }

        _ = GuestThreadExecution.Scheduler?.WakeBlockedThreads(
            GetSemaphoreWakeKey(handle),
            signalCount);
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "4DM06U2BNEY",
        ExportName = "sceKernelCancelSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelCancelSema(CpuContext ctx)
    {
        var rawHandle = ctx[CpuRegister.Rdi];
        var setCount = unchecked((int)ctx[CpuRegister.Rsi]);
        var waitingThreadsAddress = ctx[CpuRegister.Rdx];

        if (!TryResolveSemaphore(ctx, rawHandle, out var handle, out var semaphore))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        if (setCount > semaphore.MaxCount)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        lock (semaphore.Gate)
        {
            if (waitingThreadsAddress != 0 && !TryWriteUInt32(ctx, waitingThreadsAddress, unchecked((uint)semaphore.WaitingThreads)))
            {
                return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
            }

            semaphore.Count = setCount < 0 ? semaphore.InitialCount : setCount;
            semaphore.WaitingThreads = 0;
            TraceSemaphore($"cancel handle=0x{handle:X8} name='{semaphore.Name}' set={setCount} count={semaphore.Count}");
        }

        _ = GuestThreadExecution.Scheduler?.WakeBlockedThreads(GetSemaphoreWakeKey(handle));
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "R1Jvn8bSCW8",
        ExportName = "sceKernelDeleteSema",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelDeleteSema(CpuContext ctx)
    {
        var rawHandle = ctx[CpuRegister.Rdi];
        if (!TryResolveSemaphore(ctx, rawHandle, out var handle, out var semaphore))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        // Guest waits are cooperatively scheduled rather than blocked inside this call. Keep the
        // state as a tombstone so a waiter that was already scheduled before delete can still
        // consume the final signal instead of observing NOT_FOUND and entering a fatal error path.
        lock (semaphore.Gate)
        {
            semaphore.IsDeleted = true;
            semaphore.Count = 0;
        }

        _ = GuestThreadExecution.Scheduler?.WakeBlockedThreads(GetSemaphoreWakeKey(handle));
        TraceSemaphore($"delete handle=0x{handle:X8} name='{semaphore.Name}'");
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    private static bool TryResolveSemaphore(
        CpuContext ctx,
        ulong rawHandle,
        out uint handle,
        out KernelSemaphoreState semaphore)
    {
        handle = unchecked((uint)rawHandle);
        if (_semaphores.TryGetValue(handle, out semaphore!))
        {
            return true;
        }

        if (_handlesByStorageAddress.TryGetValue(rawHandle, out var storedHandle) &&
            _semaphores.TryGetValue(storedHandle, out semaphore!))
        {
            handle = storedHandle;
            TraceSemaphore(
                $"resolve-storage raw=0x{rawHandle:X16} handle=0x{handle:X8}");
            return true;
        }

        // Some PS5 middleware wrappers pass the address of their SceKernelSema storage instead of
        // the scalar value. Accept that ABI shape when the pointed-to value names a live semaphore.
        if (rawHandle > uint.MaxValue &&
            TryReadUInt32(ctx, rawHandle, out var indirectHandle))
        {
            if (_semaphores.TryGetValue(indirectHandle, out semaphore!))
            {
                handle = indirectHandle;
                return true;
            }

            TraceSemaphore(
                $"resolve-miss raw=0x{rawHandle:X16} low=0x{handle:X8} indirect=0x{indirectHandle:X8}");

            // Some wrappers store a 32-bit address relative to their 4-GiB guest arena.
            var relativeNestedAddress = (rawHandle & 0xFFFF_FFFF_0000_0000UL) | indirectHandle;
            if (relativeNestedAddress != rawHandle)
            {
                if (_handlesByStorageAddress.TryGetValue(relativeNestedAddress, out storedHandle) &&
                    _semaphores.TryGetValue(storedHandle, out semaphore!))
                {
                    handle = storedHandle;
                    TraceSemaphore(
                        $"resolve-relative-storage raw=0x{rawHandle:X16} nested=0x{relativeNestedAddress:X16} handle=0x{handle:X8}");
                    return true;
                }

                if (TryReadUInt32(ctx, relativeNestedAddress, out var relativeNestedHandle) &&
                    _semaphores.TryGetValue(relativeNestedHandle, out semaphore!))
                {
                    handle = relativeNestedHandle;
                    TraceSemaphore(
                        $"resolve-relative raw=0x{rawHandle:X16} nested=0x{relativeNestedAddress:X16} handle=0x{handle:X8}");
                    return true;
                }

            }
        }

        if (rawHandle > uint.MaxValue &&
            ctx.TryReadUInt64(rawHandle, out var nestedAddress) &&
            nestedAddress > uint.MaxValue &&
            nestedAddress != rawHandle)
        {
            if (_handlesByStorageAddress.TryGetValue(nestedAddress, out storedHandle) &&
                _semaphores.TryGetValue(storedHandle, out semaphore!))
            {
                handle = storedHandle;
                TraceSemaphore(
                    $"resolve-nested-storage raw=0x{rawHandle:X16} nested=0x{nestedAddress:X16} handle=0x{handle:X8}");
                return true;
            }

            if (TryReadUInt32(ctx, nestedAddress, out var nestedHandle) &&
                _semaphores.TryGetValue(nestedHandle, out semaphore!))
            {
                handle = nestedHandle;
                TraceSemaphore(
                    $"resolve-nested raw=0x{rawHandle:X16} nested=0x{nestedAddress:X16} handle=0x{handle:X8}");
                return true;
            }
        }

        semaphore = null!;
        return false;
    }

    private static int SetReturn(CpuContext ctx, OrbisGen2Result result)
    {
        var value = (int)result;
        ctx[CpuRegister.Rax] = unchecked((ulong)value);
        return value;
    }

    private static bool TryReadUInt32(CpuContext ctx, ulong address, out uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        if (!ctx.Memory.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }

    private static bool TryWriteUInt32(CpuContext ctx, ulong address, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        return ctx.Memory.TryWrite(address, buffer);
    }

    [SysAbiExport(
        Nid = "__hle_sem_init",
        ExportName = "sem_init",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemInit(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_destroy",
        ExportName = "sem_destroy",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemDestroy(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_post",
        ExportName = "sem_post",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemPost(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_wait",
        ExportName = "sem_wait",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemWait(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_trywait",
        ExportName = "sem_trywait",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemTryWait(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_timedwait",
        ExportName = "sem_timedwait",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemTimedWait(CpuContext ctx) => SetPosixSuccess(ctx);

    [SysAbiExport(
        Nid = "__hle_sem_getvalue",
        ExportName = "sem_getvalue",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixSemGetValue(CpuContext ctx)
    {
        var outputAddress = ctx[CpuRegister.Rsi];
        if (outputAddress != 0)
        {
            _ = TryWriteUInt32(ctx, outputAddress, 1);
        }
        return SetPosixSuccess(ctx);
    }

    private static int SetPosixSuccess(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static bool TryReadNullTerminatedUtf8(CpuContext ctx, ulong address, int maxLength, out string value)
    {
        value = string.Empty;
        if (address == 0 || maxLength <= 0)
        {
            return false;
        }

        var bytes = new byte[Math.Min(maxLength, 4096)];
        Span<byte> current = stackalloc byte[1];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!ctx.Memory.TryRead(address + (ulong)i, current))
            {
                return false;
            }

            if (current[0] == 0)
            {
                value = Encoding.UTF8.GetString(bytes, 0, i);
                return true;
            }

            bytes[i] = current[0];
        }

        value = Encoding.UTF8.GetString(bytes);
        return true;
    }

    private static void TraceSemaphore(string message)
    {
        if (ShouldTraceSemaphore())
        {
            Log.Trace($"sema.{message}");
        }
    }

    private static bool ShouldTraceSemaphore() =>
        string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_LOG_SEMA"), "1", StringComparison.Ordinal);

    private static string GetSemaphoreWakeKey(uint handle) =>
        $"semaphore:0x{handle:X8}";

    private static bool HasFmodSemaphoreTombstone()
    {
        foreach (var semaphore in _semaphores.Values)
        {
            if (string.Equals(semaphore.Name, "FMOD Semaphore", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
