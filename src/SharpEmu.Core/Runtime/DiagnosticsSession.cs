// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;

namespace SharpEmu.Core.Runtime;

/// <summary>
/// Structured description of what the CPU was doing when a run ended. Populated by the runtime from
/// whichever trap/fault/not-implemented path fired; the host adds a <c>HostException</c> variant when
/// the failure came from the emulator itself rather than from guest code.
/// </summary>
public sealed record DiagnosticsCrashContext(
    string Kind,
    ulong? Rip,
    byte? Opcode,
    ulong? FaultAddress,
    int? FaultSize,
    bool? FaultIsWrite,
    string? Nid,
    string? ExportName,
    string? LibraryName,
    string? Detail);

/// <summary>
/// Snapshot of everything a diagnostics dump needs for a single run. The runtime fills the emulator-side
/// data via <see cref="ISharpEmuRuntime.CaptureDiagnostics"/>; the host (CLI) fills the process-side data
/// (<see cref="BootLogText"/>, <see cref="HostExceptionText"/>, <see cref="CommandLine"/>) before handing
/// the session to <see cref="DiagnosticsSessionWriter"/>.
/// </summary>
public sealed class DiagnosticsSession
{
    public required string TitleId { get; init; }

    public string? Title { get; init; }

    public string? Version { get; init; }

    public OrbisGen2Result? Result { get; init; }

    public string? DiagnosticsText { get; init; }

    public string? SessionSummary { get; init; }

    public string? MilestoneLog { get; init; }

    public string? ImportTrace { get; init; }

    public DiagnosticsCrashContext? Crash { get; init; }

    public IReadOnlyList<VirtualMemoryRegion> MemoryRegions { get; init; } = Array.Empty<VirtualMemoryRegion>();

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    public string? CommandLine { get; set; }

    public string? BootLogText { get; set; }

    public string? HostExceptionText { get; set; }
}
