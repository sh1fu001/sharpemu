// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Runtime.CompilerServices;

namespace SharpEmu.Logging;

public sealed class SharpEmuLogger
{
    public SharpEmuLogger(string category)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
    }

    public string Category { get; }

    public bool IsEnabled(LogLevel level) => SharpEmuLog.IsEnabled(level);

    // Zero-allocation interpolated overloads: when the level is disabled the
    // interpolated string is never built (see *LogInterpolatedStringHandler).

    public void Trace(
        [InterpolatedStringHandlerArgument] ref TraceLogInterpolatedStringHandler handler,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Trace, handler.ToStringAndClear(), exception: null, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Debug(
        [InterpolatedStringHandlerArgument] ref DebugLogInterpolatedStringHandler handler,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Debug, handler.ToStringAndClear(), exception: null, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Info(
        [InterpolatedStringHandlerArgument] ref InfoLogInterpolatedStringHandler handler,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Info, handler.ToStringAndClear(), exception: null, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Warning(
        [InterpolatedStringHandlerArgument] ref WarningLogInterpolatedStringHandler handler,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Warning, handler.ToStringAndClear(), exception: null, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Error(
        ref ErrorLogInterpolatedStringHandler handler,
        Exception? exception = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Error, handler.ToStringAndClear(), exception, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Critical(
        ref CriticalLogInterpolatedStringHandler handler,
        Exception? exception = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
    {
        if (handler.Enabled)
        {
            Write(LogLevel.Critical, handler.ToStringAndClear(), exception, sourceFilePath, sourceLine, sourceMemberName);
        }
    }

    public void Trace(
        string message,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Trace, message, exception: null, sourceFilePath, sourceLine, sourceMemberName);

    public void Debug(
        string message,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Debug, message, exception: null, sourceFilePath, sourceLine, sourceMemberName);

    public void Info(
        string message,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Info, message, exception: null, sourceFilePath, sourceLine, sourceMemberName);

    public void Warn(
        string message,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Warning(message, sourceFilePath, sourceLine, sourceMemberName);

    public void Warning(
        string message,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Warning, message, exception: null, sourceFilePath, sourceLine, sourceMemberName);

    public void Error(
        string message,
        Exception? exception = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Error, message, exception, sourceFilePath, sourceLine, sourceMemberName);

    public void Critical(
        string message,
        Exception? exception = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string sourceMemberName = "")
        => Write(LogLevel.Critical, message, exception, sourceFilePath, sourceLine, sourceMemberName);

    private void Write(
        LogLevel level,
        string message,
        Exception? exception,
        string sourceFilePath,
        int sourceLine,
        string sourceMemberName)
    {
        SharpEmuLog.Write(
            level,
            Category,
            message,
            exception,
            sourceFilePath,
            sourceLine,
            sourceMemberName);
    }
}
