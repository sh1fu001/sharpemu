// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using SharpEmu.Logging;

namespace SharpEmu.Core.Runtime;

/// <summary>
/// Writes a per-run diagnostics session to <c>logs/&lt;TITLE_ID&gt;/&lt;yyyy-MM-dd_HH-mm-ss&gt;/</c>, turning the
/// text and structured facts collected during a run into machine-readable JSON plus a captured boot log.
/// The point is that when a game stalls you can see exactly which NID, syscall, module, memory region and
/// GPU submit were involved rather than only "it crashed".
/// </summary>
public static class DiagnosticsSessionWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes the session to disk. Returns the created directory, or <see langword="null"/> (with
    /// <paramref name="error"/> set) if writing failed. Never throws.
    /// </summary>
    public static string? TryWrite(DiagnosticsSession session, out string? error, string? logsRootOverride = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            var sessionDirectory = CreateSessionDirectory(session, logsRootOverride);
            File.WriteAllText(Path.Combine(sessionDirectory, "boot.log"), session.BootLogText ?? string.Empty);
            WriteJson(sessionDirectory, "imports_missing.json", BuildMissingImports(session));
            WriteJson(sessionDirectory, "syscalls.json", BuildSyscalls());
            WriteJson(sessionDirectory, "modules.json", BuildModules());
            WriteJson(sessionDirectory, "memory_map.json", BuildMemoryMap(session));
            WriteJson(sessionDirectory, "gpu_submits.json", BuildGpuSubmits());
            WriteJson(sessionDirectory, "crash_context.json", BuildCrashContext(session));
            error = null;
            return sessionDirectory;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return null;
        }
    }

    private static string CreateSessionDirectory(DiagnosticsSession session, string? logsRootOverride)
    {
        var logsRoot = string.IsNullOrWhiteSpace(logsRootOverride) ? ResolveLogsRoot() : logsRootOverride;
        var titleFolder = SanitizeTitleId(session.TitleId);
        var stamp = session.StartedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        var baseDirectory = Path.Combine(logsRoot, titleFolder, stamp);
        var directory = baseDirectory;
        var suffix = 2;
        while (Directory.Exists(directory))
        {
            directory = $"{baseDirectory}_{suffix++}";
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteJson(string directory, string fileName, object payload)
    {
        File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static MissingImportsDto BuildMissingImports(DiagnosticsSession session)
    {
        var records = RunDiagnostics.SnapshotMissingImports();
        return new MissingImportsDto(
            session.TitleId,
            RunDiagnostics.MissingImportTotal,
            records.Count,
            records
                .Select(record => new MissingImportDto(
                    record.Nid,
                    record.Library,
                    record.Name,
                    record.Count,
                    Hex(record.FirstReturnRip)))
                .ToArray());
    }

    private static SyscallsDto BuildSyscalls()
    {
        var records = RunDiagnostics.SnapshotSyscalls();
        return new SyscallsDto(
            RunDiagnostics.SyscallTotal,
            records.Count,
            records
                .Select(record => new SyscallDto(
                    record.Number,
                    $"0x{record.Number:X}",
                    record.Count,
                    new[] { Hex(record.Arg1), Hex(record.Arg2), Hex(record.Arg3), Hex(record.Arg4) }))
                .ToArray());
    }

    private static ModulesDto BuildModules()
    {
        var modules = new List<ModuleDto>();
        foreach (var handle in KernelModuleRegistry.GetModuleHandles(includeSystemModules: true))
        {
            if (!KernelModuleRegistry.TryGetModuleByHandle(handle, out var module))
            {
                continue;
            }

            modules.Add(new ModuleDto(
                module.Handle,
                module.Name,
                string.IsNullOrEmpty(module.Path) ? null : module.Path,
                Hex(module.BaseAddress),
                Hex(module.EndAddress),
                Hex(module.EntryPoint),
                module.IsMain,
                module.IsSystemModule));
        }

        return new ModulesDto(modules.Count, modules);
    }

    private static MemoryMapDto BuildMemoryMap(DiagnosticsSession session)
    {
        var regions = session.MemoryRegions
            .OrderBy(region => region.VirtualAddress)
            .Select(region => new MemoryRegionDto(
                Hex(region.VirtualAddress),
                Hex(unchecked(region.VirtualAddress + region.MemorySize)),
                region.MemorySize,
                region.FileOffset,
                region.FileSize,
                FormatProtection(region.Protection),
                (uint)region.Protection))
            .ToArray();
        return new MemoryMapDto(regions.Length, regions);
    }

    private static GpuSubmitsDto BuildGpuSubmits()
    {
        var records = RunDiagnostics.SnapshotGpuSubmits();
        return new GpuSubmitsDto(
            RunDiagnostics.GpuSubmitTotal,
            records.Count,
            records
                .Select(record => new GpuSubmitDto(
                    record.Index,
                    record.Kind,
                    Hex(record.CommandAddress),
                    record.DwordCount,
                    record.QueueId))
                .ToArray());
    }

    private static CrashContextDto BuildCrashContext(DiagnosticsSession session)
    {
        var result = session.Result;
        var meta = new SessionMetaDto(
            session.TitleId,
            session.Title,
            session.Version,
            session.StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            session.CommandLine,
            result?.ToString() ?? "Unknown",
            result.HasValue ? $"0x{(int)result.Value:X8}" : null);

        CrashDto? crash = null;
        if (session.Crash is { } source)
        {
            crash = new CrashDto(
                source.Kind,
                source.Rip.HasValue ? Hex(source.Rip.Value) : null,
                source.Opcode.HasValue ? $"0x{source.Opcode.Value:X2}" : null,
                source.FaultAddress.HasValue ? Hex(source.FaultAddress.Value) : null,
                source.FaultSize,
                source.FaultIsWrite,
                source.Nid,
                source.ExportName,
                source.LibraryName,
                source.Detail);
        }

        return new CrashContextDto(
            meta,
            crash,
            session.HostExceptionText,
            session.SessionSummary,
            session.DiagnosticsText);
    }

    private static string ResolveLogsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SharpEmu.slnx")))
            {
                return Path.Combine(current.FullName, "logs");
            }

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "logs");
    }

    private static string SanitizeTitleId(string? titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId))
        {
            return "UNKNOWN";
        }

        var builder = new StringBuilder(titleId.Length);
        foreach (var character in titleId.Trim())
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '_');
        }

        var sanitized = builder.ToString().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(sanitized) ? "UNKNOWN" : sanitized;
    }

    private static string FormatProtection(ProgramHeaderFlags protection)
    {
        Span<char> flags =
        [
            (protection & ProgramHeaderFlags.Read) != 0 ? 'R' : '-',
            (protection & ProgramHeaderFlags.Write) != 0 ? 'W' : '-',
            (protection & ProgramHeaderFlags.Execute) != 0 ? 'X' : '-',
        ];
        return new string(flags);
    }

    private static string Hex(ulong value) => $"0x{value:X16}";

    private sealed record MissingImportsDto(
        string TitleId,
        long TotalMissingHits,
        int DistinctCount,
        IReadOnlyList<MissingImportDto> Imports);

    private sealed record MissingImportDto(
        string Nid,
        string? Library,
        string? Name,
        long Count,
        string FirstReturnRip);

    private sealed record SyscallsDto(
        long TotalSyscallHits,
        int DistinctCount,
        IReadOnlyList<SyscallDto> Syscalls);

    private sealed record SyscallDto(
        ulong Number,
        string NumberHex,
        long Count,
        IReadOnlyList<string> Args);

    private sealed record ModulesDto(
        int Count,
        IReadOnlyList<ModuleDto> Modules);

    private sealed record ModuleDto(
        int Handle,
        string Name,
        string? Path,
        string BaseAddress,
        string EndAddress,
        string EntryPoint,
        bool IsMain,
        bool IsSystemModule);

    private sealed record MemoryMapDto(
        int Count,
        IReadOnlyList<MemoryRegionDto> Regions);

    private sealed record MemoryRegionDto(
        string VirtualAddress,
        string EndAddress,
        ulong Size,
        ulong FileOffset,
        ulong FileSize,
        string Protection,
        uint ProtectionFlags);

    private sealed record GpuSubmitsDto(
        long TotalSubmits,
        int SampleCount,
        IReadOnlyList<GpuSubmitDto> Submits);

    private sealed record GpuSubmitDto(
        long Index,
        string Kind,
        string CommandAddress,
        uint DwordCount,
        uint QueueId);

    private sealed record CrashContextDto(
        SessionMetaDto Session,
        CrashDto? Crash,
        string? HostException,
        string? SessionSummary,
        string? DiagnosticsText);

    private sealed record SessionMetaDto(
        string TitleId,
        string? Title,
        string? Version,
        string StartedAt,
        string? CommandLine,
        string Result,
        string? ResultHex);

    private sealed record CrashDto(
        string Kind,
        string? Rip,
        string? Opcode,
        string? FaultAddress,
        int? FaultSize,
        bool? FaultIsWrite,
        string? Nid,
        string? ExportName,
        string? LibraryName,
        string? Detail);
}
