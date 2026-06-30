// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using Xunit;

// Several components touch process-global state (SharpEmuLog's sink/level,
// real Win32 allocations). The suite is tiny, so run tests sequentially to
// keep them deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
