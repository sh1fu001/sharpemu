// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Logging;

public enum LogLevel
{
    // Finest level, below Trace. Renumbered cleanly so the enum still starts at
    // 0 and every `level >= _minimumLevel` filter keeps working unchanged. The
    // default MinimumLevel remains Info, so adding Verbose changes no default
    // behavior — it only becomes selectable via --log-level=verbose.
    Verbose = 0,

    Trace = 1,

    Debug = 2,

    Info = 3,

    Warning = 4,

    Error = 5,

    Critical = 6,

    None = 7,
}
