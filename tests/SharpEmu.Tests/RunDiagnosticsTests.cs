// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Logging;
using Xunit;

namespace SharpEmu.Tests;

// RunDiagnostics is a process-wide collector, so these tests must not run alongside anything else
// that records into it.
[CollectionDefinition("DiagnosticsState", DisableParallelization = true)]
public sealed class DiagnosticsStateCollection;

[Collection("DiagnosticsState")]
public sealed class RunDiagnosticsTests
{
    public RunDiagnosticsTests() => RunDiagnostics.Reset();

    [Fact]
    public void RecordMissingImport_Deduplicates_ByNidAndCountsHits()
    {
        RunDiagnostics.RecordMissingImport("NID_A", "libkernel", "sceFoo", 0x1000);
        RunDiagnostics.RecordMissingImport("NID_A", "libkernel", "sceFoo", 0x2000);
        RunDiagnostics.RecordMissingImport("NID_B", null, null, 0x3000);

        var imports = RunDiagnostics.SnapshotMissingImports();

        Assert.Equal(2, imports.Count);
        Assert.Equal(3, RunDiagnostics.MissingImportTotal);
        var first = Assert.Single(imports, record => record.Nid == "NID_A");
        Assert.Equal(2, first.Count);
        Assert.Equal(0x1000ul, first.FirstReturnRip); // keeps the first observed return address
        Assert.Equal("libkernel", first.Library);
    }

    [Fact]
    public void SnapshotMissingImports_OrdersByCountDescending()
    {
        RunDiagnostics.RecordMissingImport("RARE", null, null, 0);
        RunDiagnostics.RecordMissingImport("HOT", null, null, 0);
        RunDiagnostics.RecordMissingImport("HOT", null, null, 0);

        var imports = RunDiagnostics.SnapshotMissingImports();

        Assert.Equal("HOT", imports[0].Nid);
        Assert.Equal("RARE", imports[1].Nid);
    }

    [Fact]
    public void RecordSyscall_Deduplicates_ByNumber()
    {
        RunDiagnostics.RecordSyscall(202, 1, 2, 3, 4);
        RunDiagnostics.RecordSyscall(202, 9, 9, 9, 9);
        RunDiagnostics.RecordSyscall(20, 0, 0, 0, 0);

        var syscalls = RunDiagnostics.SnapshotSyscalls();

        Assert.Equal(2, syscalls.Count);
        Assert.Equal(3, RunDiagnostics.SyscallTotal);
        var sysctl = Assert.Single(syscalls, record => record.Number == 202);
        Assert.Equal(2, sysctl.Count);
        Assert.Equal(1ul, sysctl.Arg1); // keeps first-seen arguments
    }

    [Fact]
    public void RecordGpuSubmit_KeepsRecentTail_AndTotalCount()
    {
        RunDiagnostics.RecordGpuSubmit("dcb", 0x4000, 128, 0);
        RunDiagnostics.RecordGpuSubmit("acb", 0x5000, 64, 7);

        var submits = RunDiagnostics.SnapshotGpuSubmits();

        Assert.Equal(2, submits.Count);
        Assert.Equal(2, RunDiagnostics.GpuSubmitTotal);
        Assert.Equal("dcb", submits[0].Kind);
        Assert.Equal(1, submits[0].Index);
        Assert.Equal("acb", submits[1].Kind);
        Assert.Equal(7u, submits[1].QueueId);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        RunDiagnostics.RecordMissingImport("NID", null, null, 0);
        RunDiagnostics.RecordSyscall(1, 0, 0, 0, 0);
        RunDiagnostics.RecordGpuSubmit("dcb", 0, 0, 0);

        RunDiagnostics.Reset();

        Assert.Empty(RunDiagnostics.SnapshotMissingImports());
        Assert.Empty(RunDiagnostics.SnapshotSyscalls());
        Assert.Empty(RunDiagnostics.SnapshotGpuSubmits());
        Assert.Equal(0, RunDiagnostics.MissingImportTotal);
        Assert.Equal(0, RunDiagnostics.SyscallTotal);
        Assert.Equal(0, RunDiagnostics.GpuSubmitTotal);
    }
}
