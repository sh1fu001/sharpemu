// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpEmu.Logging;

/// <summary>A guest import (NID) that was hit but had no HLE implementation.</summary>
public sealed record MissingImportRecord(
    string Nid,
    string? Library,
    string? Name,
    long Count,
    ulong FirstReturnRip);

/// <summary>A raw guest syscall observed during execution.</summary>
public sealed record SyscallRecord(
    ulong Number,
    long Count,
    ulong Arg1,
    ulong Arg2,
    ulong Arg3,
    ulong Arg4);

/// <summary>A GPU command-buffer submit (AGC DCB/ACB) observed during execution.</summary>
public sealed record GpuSubmitRecord(
    long Index,
    string Kind,
    ulong CommandAddress,
    uint DwordCount,
    uint QueueId);

/// <summary>A distinct guest shader program the GPU pipeline bound during execution.</summary>
public sealed record ShaderRecord(
    string Stage,
    ulong Address,
    int DwordCount,
    ulong Hash);

/// <summary>
/// Process-wide, thread-safe collector for the structured facts a diagnostics session cares about:
/// which NID was missing, which syscall was hit, and which GPU submit was in flight when a game stalls.
/// Collection points are on cold paths (unresolved imports, payload syscalls, GPU submits), so the
/// bookkeeping cost is negligible. Call <see cref="Reset"/> at the start of each run.
/// </summary>
public static class RunDiagnostics
{
    private const int MaxDistinctMissingImports = 1024;
    private const int MaxDistinctSyscalls = 512;
    private const int MaxGpuSubmitSamples = 512;
    private const int MaxDistinctShaders = 4096;

    private static readonly object _gate = new();
    private static readonly Dictionary<string, MissingImportRecord> _missingImports = new(StringComparer.Ordinal);
    private static readonly Dictionary<ulong, SyscallRecord> _syscalls = new();
    private static readonly Queue<GpuSubmitRecord> _gpuSubmits = new();
    private static readonly Dictionary<(string Stage, ulong Address), ShaderRecord> _shaders = new();
    private static long _missingImportTotal;
    private static long _syscallTotal;
    private static long _gpuSubmitTotal;

    public static void Reset()
    {
        lock (_gate)
        {
            _missingImports.Clear();
            _syscalls.Clear();
            _gpuSubmits.Clear();
            _shaders.Clear();
            _missingImportTotal = 0;
            _syscallTotal = 0;
            _gpuSubmitTotal = 0;
        }
    }

    public static void RecordMissingImport(string? nid, string? library, string? name, ulong returnRip)
    {
        if (string.IsNullOrEmpty(nid))
        {
            return;
        }

        lock (_gate)
        {
            _missingImportTotal++;
            if (_missingImports.TryGetValue(nid, out var existing))
            {
                _missingImports[nid] = existing with { Count = existing.Count + 1 };
                return;
            }

            if (_missingImports.Count >= MaxDistinctMissingImports)
            {
                return;
            }

            _missingImports[nid] = new MissingImportRecord(nid, library, name, 1, returnRip);
        }
    }

    public static void RecordSyscall(ulong number, ulong arg1, ulong arg2, ulong arg3, ulong arg4)
    {
        lock (_gate)
        {
            _syscallTotal++;
            if (_syscalls.TryGetValue(number, out var existing))
            {
                _syscalls[number] = existing with { Count = existing.Count + 1 };
                return;
            }

            if (_syscalls.Count >= MaxDistinctSyscalls)
            {
                return;
            }

            _syscalls[number] = new SyscallRecord(number, 1, arg1, arg2, arg3, arg4);
        }
    }

    public static void RecordGpuSubmit(string kind, ulong commandAddress, uint dwordCount, uint queueId)
    {
        lock (_gate)
        {
            var index = ++_gpuSubmitTotal;
            _gpuSubmits.Enqueue(new GpuSubmitRecord(index, kind, commandAddress, dwordCount, queueId));
            while (_gpuSubmits.Count > MaxGpuSubmitSamples)
            {
                _gpuSubmits.Dequeue();
            }
        }
    }

    public static IReadOnlyList<MissingImportRecord> SnapshotMissingImports()
    {
        lock (_gate)
        {
            return _missingImports.Values
                .OrderByDescending(record => record.Count)
                .ThenBy(record => record.Nid, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static IReadOnlyList<SyscallRecord> SnapshotSyscalls()
    {
        lock (_gate)
        {
            return _syscalls.Values
                .OrderByDescending(record => record.Count)
                .ThenBy(record => record.Number)
                .ToArray();
        }
    }

    public static void RecordShader(string stage, ulong address, int dwordCount, ulong hash)
    {
        if (string.IsNullOrEmpty(stage) || address == 0)
        {
            return;
        }

        lock (_gate)
        {
            var key = (stage, address);
            if (_shaders.ContainsKey(key) || _shaders.Count >= MaxDistinctShaders)
            {
                return;
            }

            _shaders[key] = new ShaderRecord(stage, address, dwordCount, hash);
        }
    }

    public static IReadOnlyList<GpuSubmitRecord> SnapshotGpuSubmits()
    {
        lock (_gate)
        {
            return _gpuSubmits.ToArray();
        }
    }

    public static IReadOnlyList<ShaderRecord> SnapshotShaders()
    {
        lock (_gate)
        {
            return _shaders.Values
                .OrderBy(record => record.Stage, StringComparer.Ordinal)
                .ThenBy(record => record.Address)
                .ToArray();
        }
    }

    public static long MissingImportTotal
    {
        get
        {
            lock (_gate)
            {
                return _missingImportTotal;
            }
        }
    }

    public static long SyscallTotal
    {
        get
        {
            lock (_gate)
            {
                return _syscallTotal;
            }
        }
    }

    public static long GpuSubmitTotal
    {
        get
        {
            lock (_gate)
            {
                return _gpuSubmitTotal;
            }
        }
    }
}
