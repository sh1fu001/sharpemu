// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace SharpEmu.Core.Memory;

/// <summary>
/// Thrown by the checked <see cref="GuestAddressSpace"/> accessors when an access is rejected. Carries the
/// structured <see cref="MemoryAccessViolation"/> so callers can render the diagnostic block or inspect it.
/// </summary>
public sealed class MemoryAccessException : Exception
{
    public MemoryAccessException(MemoryAccessViolation violation)
        : base(violation.Format())
    {
        Violation = violation;
    }

    public MemoryAccessViolation Violation { get; }
}
