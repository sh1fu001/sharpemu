// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text.Json;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.Core.Runtime;
using SharpEmu.HLE;
using SharpEmu.Logging;
using Xunit;

namespace SharpEmu.Tests;

[Collection("DiagnosticsState")]
public sealed class DiagnosticsSessionWriterTests
{
    [Fact]
    public void TryWrite_ProducesAllSessionFiles_WithStructuredContent()
    {
        RunDiagnostics.Reset();
        RunDiagnostics.RecordMissingImport("NID_MISSING", "libSceFoo", "sceFoo", 0x1234);
        RunDiagnostics.RecordSyscall(202, 1, 2, 3, 4);
        RunDiagnostics.RecordGpuSubmit("dcb", 0xDEAD0000, 256, 0);

        var session = new DiagnosticsSession
        {
            TitleId = "PPSA01341",
            Title = "Example",
            Version = "01.00",
            Result = OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_IMPLEMENTED,
            DiagnosticsText = "Not implemented: nid=NID_MISSING",
            SessionSummary = "Summary: result=NOT_IMPLEMENTED",
            Crash = new DiagnosticsCrashContext(
                "NotImplemented", 0x400010, null, null, null, null, "NID_MISSING", "sceFoo", "libSceFoo", "stub"),
            MemoryRegions = new[]
            {
                new VirtualMemoryRegion(0x400000, 0x1000, 0, 0x800, ProgramHeaderFlags.Read | ProgramHeaderFlags.Execute),
            },
            BootLogText = "hello boot log",
            CommandLine = "SharpEmu eboot.bin",
        };

        var logsRoot = Path.Combine(Path.GetTempPath(), "sharpemu-diag-" + Guid.NewGuid().ToString("N"));
        try
        {
            var directory = DiagnosticsSessionWriter.TryWrite(session, out var error, logsRoot);

            Assert.Null(error);
            Assert.NotNull(directory);
            Assert.True(Directory.Exists(directory));
            Assert.StartsWith(Path.Combine(logsRoot, "PPSA01341"), directory);

            foreach (var expected in new[]
                     {
                         "boot.log",
                         "imports_missing.json",
                         "syscalls.json",
                         "modules.json",
                         "memory_map.json",
                         "gpu_submits.json",
                         "crash_context.json",
                     })
            {
                Assert.True(File.Exists(Path.Combine(directory!, expected)), $"missing {expected}");
            }

            Assert.Equal("hello boot log", File.ReadAllText(Path.Combine(directory!, "boot.log")));

            using var missing = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "imports_missing.json")));
            var import = Assert.Single(missing.RootElement.GetProperty("imports").EnumerateArray());
            Assert.Equal("NID_MISSING", import.GetProperty("nid").GetString());
            Assert.Equal("libSceFoo", import.GetProperty("library").GetString());

            using var memory = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "memory_map.json")));
            var region = Assert.Single(memory.RootElement.GetProperty("regions").EnumerateArray());
            Assert.Equal("R-X", region.GetProperty("protection").GetString());

            using var crash = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "crash_context.json")));
            Assert.Equal("PPSA01341", crash.RootElement.GetProperty("session").GetProperty("titleId").GetString());
            Assert.Equal("NotImplemented", crash.RootElement.GetProperty("crash").GetProperty("kind").GetString());
            Assert.Equal("NID_MISSING", crash.RootElement.GetProperty("crash").GetProperty("nid").GetString());

            using var gpu = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "gpu_submits.json")));
            Assert.Equal(1, gpu.RootElement.GetProperty("totalSubmits").GetInt64());
        }
        finally
        {
            if (Directory.Exists(logsRoot))
            {
                Directory.Delete(logsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TryWrite_SanitizesMissingTitleId_ToUnknown()
    {
        RunDiagnostics.Reset();
        var session = new DiagnosticsSession { TitleId = "UNKNOWN" };
        var logsRoot = Path.Combine(Path.GetTempPath(), "sharpemu-diag-" + Guid.NewGuid().ToString("N"));
        try
        {
            var directory = DiagnosticsSessionWriter.TryWrite(session, out var error, logsRoot);

            Assert.Null(error);
            Assert.NotNull(directory);
            Assert.StartsWith(Path.Combine(logsRoot, "UNKNOWN"), directory);
        }
        finally
        {
            if (Directory.Exists(logsRoot))
            {
                Directory.Delete(logsRoot, recursive: true);
            }
        }
    }
}
