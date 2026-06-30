// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Logging;
using Xunit;

namespace SharpEmu.Tests;

// These tests mutate SharpEmuLog's global level/sink, so they run in a
// non-parallel collection to avoid interfering with each other.
[CollectionDefinition("SharpEmuLogState", DisableParallelization = true)]
public sealed class SharpEmuLogStateCollection;

[Collection("SharpEmuLogState")]
public sealed class LoggingHandlerTests : IDisposable
{
    private sealed class CapturingSink : ISharpEmuLogSink
    {
        public List<LogEntry> Entries { get; } = new();

        public void Write(in LogEntry entry) => Entries.Add(entry);
    }

    private readonly LogLevel _originalLevel = SharpEmuLog.MinimumLevel;
    private readonly ISharpEmuLogSink _originalSink = SharpEmuLog.Sink;
    private readonly CapturingSink _sink = new();

    public LoggingHandlerTests()
    {
        SharpEmuLog.Sink = _sink;
    }

    public void Dispose()
    {
        SharpEmuLog.MinimumLevel = _originalLevel;
        SharpEmuLog.Sink = _originalSink;
    }

    [Fact]
    public void Info_WhenLevelEnabled_WritesFormattedEntry()
    {
        SharpEmuLog.MinimumLevel = LogLevel.Trace;
        var log = SharpEmuLog.For("UnitTest");

        log.Info($"value={42:X2}");

        var entry = Assert.Single(_sink.Entries);
        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal("UnitTest", entry.Category);
        Assert.Equal("value=2A", entry.Message);
    }

    [Fact]
    public void Trace_WhenLevelDisabled_SkipsInterpolationAndWritesNothing()
    {
        SharpEmuLog.MinimumLevel = LogLevel.Info; // Trace < Info => disabled
        var log = SharpEmuLog.For("UnitTest");

        var evaluated = false;
        log.Trace($"{SideEffect(ref evaluated)}");

        Assert.False(evaluated); // interpolation argument must not be evaluated
        Assert.Empty(_sink.Entries);
    }

    [Fact]
    public void Error_WhenLevelNone_WritesNothing()
    {
        SharpEmuLog.MinimumLevel = LogLevel.None;
        var log = SharpEmuLog.For("UnitTest");

        log.Error($"boom {Guid.NewGuid()}");

        Assert.Empty(_sink.Entries);
    }

    [Fact]
    public void Warning_RespectsExactThreshold()
    {
        SharpEmuLog.MinimumLevel = LogLevel.Warning;
        var log = SharpEmuLog.For("UnitTest");

        log.Info($"dropped");   // below threshold
        log.Warning($"kept");   // at threshold

        var entry = Assert.Single(_sink.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("kept", entry.Message);
    }

    private static string SideEffect(ref bool flag)
    {
        flag = true;
        return "evaluated";
    }
}
