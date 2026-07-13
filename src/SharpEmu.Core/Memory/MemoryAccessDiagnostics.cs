// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Threading;
using SharpEmu.Logging;

namespace SharpEmu.Core.Memory;

/// <summary>
/// Emits bounded virtual-memory diagnostics when explicitly enabled for a memory instance.
/// </summary>
internal sealed class MemoryAccessDiagnostics
{
    private const int DefaultMaximumEvents = 10_000;

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("VMEM");
    private readonly bool _includeAddress;
    private readonly bool _enabled;
    private readonly int _maximumEvents;
    private int _emittedEvents;
    private int _capWarningEmitted;

    public MemoryAccessDiagnostics(bool includeAddress = true)
    {
        _includeAddress = includeAddress;
        _enabled = string.Equals(
            Environment.GetEnvironmentVariable("SHARPEMU_LOG_MEMORY_ACCESSES"),
            "1",
            StringComparison.Ordinal);
        _maximumEvents = ResolveMaximumEvents();
    }

    public bool IsEnabled => _enabled;

    public void Access(string operation, ulong virtualAddress, int byteCount, bool succeeded, string? failureReason = null)
    {
        if (!TryReserveEvent())
        {
            return;
        }

        var outcome = succeeded ? "success" : "failure";
        var address = _includeAddress ? $" address=0x{virtualAddress:X16}" : string.Empty;
        var reason = succeeded ? string.Empty : $" reason={failureReason ?? "unspecified"}";
        TryWriteDebug($"operation={operation}{address} bytes={byteCount} outcome={outcome}{reason}");
    }

    public void Lifecycle(string operation, ulong virtualAddress, ulong byteCount, string? details = null)
    {
        if (!TryReserveEvent())
        {
            return;
        }

        var address = _includeAddress ? $" address=0x{virtualAddress:X16}" : string.Empty;
        var suffix = string.IsNullOrEmpty(details) ? string.Empty : $" details={details}";
        TryWriteDebug($"operation={operation}{address} bytes={byteCount} outcome=success{suffix}");
    }

    private bool TryReserveEvent()
    {
        if (!_enabled)
        {
            return false;
        }

        while (true)
        {
            var emittedEvents = Volatile.Read(ref _emittedEvents);
            if (emittedEvents >= _maximumEvents)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref _emittedEvents, emittedEvents + 1, emittedEvents) == emittedEvents)
            {
                return true;
            }
        }

        if (Interlocked.Exchange(ref _capWarningEmitted, 1) == 0)
        {
            TryWriteWarning($"Memory access diagnostics event cap of {_maximumEvents} reached; further VMEM entries are suppressed.");
        }

        return false;
    }

    private static int ResolveMaximumEvents()
    {
        var configured = Environment.GetEnvironmentVariable("SHARPEMU_LOG_MEMORY_MAX_EVENTS");
        return int.TryParse(configured, out var maximumEvents) && maximumEvents > 0
            ? maximumEvents
            : DefaultMaximumEvents;
    }

    private static void TryWriteDebug(string message)
    {
        try
        {
            Log.Debug(message);
        }
        catch
        {
            // Diagnostics must never alter memory operation behaviour.
        }
    }

    private static void TryWriteWarning(string message)
    {
        try
        {
            Log.Warning(message);
        }
        catch
        {
            // Diagnostics must never alter memory operation behaviour.
        }
    }
}
