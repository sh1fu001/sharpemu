// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Runtime.CompilerServices;

namespace SharpEmu.Logging;

// Interpolated string handlers used by SharpEmuLogger so that a log call such as
//   logger.Info($"x = {expensive}")
// performs no string formatting or allocation when the level is disabled. The
// trailing `out bool enabled` constructor parameter lets the compiler skip the
// interpolation arguments entirely when the level is filtered out.

[InterpolatedStringHandler]
public ref struct TraceLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public TraceLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Trace);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}

[InterpolatedStringHandler]
public ref struct DebugLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public DebugLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Debug);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}

[InterpolatedStringHandler]
public ref struct InfoLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public InfoLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Info);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}

[InterpolatedStringHandler]
public ref struct WarningLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public WarningLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Warning);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}

[InterpolatedStringHandler]
public ref struct ErrorLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public ErrorLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Error);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}

[InterpolatedStringHandler]
public ref struct CriticalLogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;

    public readonly bool Enabled;

    public CriticalLogInterpolatedStringHandler(int literalLength, int formattedCount, out bool enabled)
    {
        Enabled = enabled = SharpEmuLog.IsEnabled(LogLevel.Critical);
        _inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string s) { if (Enabled) _inner.AppendLiteral(s); }
    public void AppendFormatted<T>(T value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted<T>(T value, string? format) { if (Enabled) _inner.AppendFormatted(value, format); }
    public void AppendFormatted<T>(T value, int alignment) { if (Enabled) _inner.AppendFormatted(value, alignment); }
    public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) _inner.AppendFormatted(value, alignment, format); }
    public void AppendFormatted(string? value) { if (Enabled) _inner.AppendFormatted(value); }
    public void AppendFormatted(ReadOnlySpan<char> value) { if (Enabled) _inner.AppendFormatted(value); }
    public string ToStringAndClear() => Enabled ? _inner.ToStringAndClear() : string.Empty;
}
