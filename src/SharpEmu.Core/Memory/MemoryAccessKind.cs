// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Core.Memory;

/// <summary>The kind of access that was attempted against a guest memory address.</summary>
public enum MemoryAccessKind
{
    Read,
    Write,
    Execute,
}
