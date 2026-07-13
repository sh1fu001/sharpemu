// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SharpEmu.Tests.Memory;

public sealed class MemoryAccessDiagnosticsTests
{
    [Fact]
    public void VirtualMemory_Disabled_EmitsNoDiagnostics()
    {
        using var environment = new DiagnosticsEnvironment(enabled: false, maximumEvents: null);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        var memory = new VirtualMemory();

        memory.Map(0x1000, 0x10, 0, [], ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(0x1000, [0x2A]));
        Assert.True(memory.TryRead(0x1000, new byte[1]));

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void VirtualMemory_Enabled_EmitsSuccessfulAccessDiagnosticsWithoutPayload()
    {
        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 10);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        var memory = new VirtualMemory();

        memory.Map(0x1000, 0x10, 0, [], ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        sink.Clear();
        Assert.True(memory.TryWrite(0x1000, [0xCA, 0xFE]));

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("VMEM", entry.Category);
        Assert.Contains("operation=write", entry.Message, StringComparison.Ordinal);
        Assert.Contains("address=0x0000000000001000", entry.Message, StringComparison.Ordinal);
        Assert.Contains("bytes=2", entry.Message, StringComparison.Ordinal);
        Assert.Contains("outcome=success", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CA", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FE", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VirtualMemory_Enabled_EmitsFailedAccessDiagnostic()
    {
        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 10);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        var memory = new VirtualMemory();

        Assert.False(memory.TryRead(0x2000, new byte[4]));

        var entry = Assert.Single(sink.Entries);
        Assert.Contains("operation=read", entry.Message, StringComparison.Ordinal);
        Assert.Contains("outcome=failure", entry.Message, StringComparison.Ordinal);
        Assert.Contains("reason=unmapped_or_out_of_range", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VirtualMemory_Enabled_EnforcesEventCapAndWarnsOnce()
    {
        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 1);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        var memory = new VirtualMemory();

        Assert.False(memory.TryRead(0x2000, new byte[1]));
        Assert.False(memory.TryRead(0x2001, new byte[1]));
        Assert.False(memory.TryRead(0x2002, new byte[1]));

        Assert.Equal(2, sink.Entries.Count);
        Assert.Single(sink.Entries.Where(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public void VirtualMemory_Enabled_IgnoresThrowingDiagnosticSink()
    {
        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 10);
        using var logging = new LoggingScope(new ThrowingSink());
        var memory = new VirtualMemory();

        memory.Map(0x1000, 0x10, 0, [], ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        Assert.True(memory.TryWrite(0x1000, [0x2A]));
        Assert.True(memory.TryRead(0x1000, new byte[1]));
        Assert.False(memory.TryRead(0x2000, new byte[1]));
    }

    [Fact]
    public void PhysicalVirtualMemory_Disabled_EmitsNoDiagnostics()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var environment = new DiagnosticsEnvironment(enabled: false, maximumEvents: null);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        using var memory = new PhysicalVirtualMemory();
        var address = memory.AllocateAt(0, 0x1000, executable: false);

        Assert.True(memory.TryWrite(address, [0x2A]));
        Assert.True(memory.TryRead(address, new byte[1]));
        Assert.False(memory.TryRead(ulong.MaxValue, new byte[1]));

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void PhysicalVirtualMemory_Enabled_EmitsSuccessfulAccessDiagnostics()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 20);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        using var memory = new PhysicalVirtualMemory();
        var address = memory.AllocateAt(0, 0x1000, executable: false);
        sink.Clear();

        Assert.True(memory.TryWrite(address, [0x2A]));
        Assert.True(memory.TryRead(address, new byte[1]));

        Assert.Equal(2, sink.Entries.Count);
        Assert.All(sink.Entries, entry =>
        {
            Assert.Contains("outcome=success", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("address=", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("2A", entry.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void PhysicalVirtualMemory_Enabled_EmitsFailedAccessDiagnostic()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 10);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        using var memory = new PhysicalVirtualMemory();

        Assert.False(memory.TryWrite(ulong.MaxValue, [0x2A]));

        var entry = Assert.Single(sink.Entries);
        Assert.Contains("outcome=failure", entry.Message, StringComparison.Ordinal);
        Assert.Contains("reason=unmapped_or_protected_or_commit_failed", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("address=", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("2A", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhysicalVirtualMemory_Enabled_EmitsLifecycleDiagnostics()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var environment = new DiagnosticsEnvironment(enabled: true, maximumEvents: 20);
        var sink = new CapturingSink();
        using var logging = new LoggingScope(sink);
        using var memory = new PhysicalVirtualMemory();

        var address = memory.AllocateAt(0, 0x1000, executable: false);
        memory.Map(address, 0x1000, 0, [], ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        memory.Clear();

        Assert.Contains(sink.Entries, entry => entry.Message.Contains("operation=allocate", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, entry => entry.Message.Contains("operation=map", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, entry => entry.Message.Contains("operation=protection", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, entry => entry.Message.Contains("operation=free", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, entry => entry.Message.Contains("operation=clear", StringComparison.Ordinal));
        Assert.All(sink.Entries, entry => Assert.DoesNotContain("address=", entry.Message, StringComparison.Ordinal));
    }

    private sealed class DiagnosticsEnvironment : IDisposable
    {
        private readonly string? _enabled = Environment.GetEnvironmentVariable("SHARPEMU_LOG_MEMORY_ACCESSES");
        private readonly string? _maximumEvents = Environment.GetEnvironmentVariable("SHARPEMU_LOG_MEMORY_MAX_EVENTS");

        public DiagnosticsEnvironment(bool enabled, int? maximumEvents)
        {
            Environment.SetEnvironmentVariable("SHARPEMU_LOG_MEMORY_ACCESSES", enabled ? "1" : null);
            Environment.SetEnvironmentVariable("SHARPEMU_LOG_MEMORY_MAX_EVENTS", maximumEvents?.ToString());
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("SHARPEMU_LOG_MEMORY_ACCESSES", _enabled);
            Environment.SetEnvironmentVariable("SHARPEMU_LOG_MEMORY_MAX_EVENTS", _maximumEvents);
        }
    }

    private sealed class LoggingScope : IDisposable
    {
        private readonly LogLevel _minimumLevel = SharpEmuLog.MinimumLevel;
        private readonly ISharpEmuLogSink _sink = SharpEmuLog.Sink;

        public LoggingScope(ISharpEmuLogSink sink)
        {
            SharpEmuLog.Configure(LogLevel.Debug, sink);
        }

        public void Dispose()
        {
            SharpEmuLog.Configure(_minimumLevel, _sink);
        }
    }

    private sealed class CapturingSink : ISharpEmuLogSink
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public void Write(in LogEntry entry) => _entries.Add(entry);

        public void Clear() => _entries.Clear();
    }

    private sealed class ThrowingSink : ISharpEmuLogSink
    {
        public void Write(in LogEntry entry) => throw new InvalidOperationException("Synthetic test sink failure.");
    }
}
