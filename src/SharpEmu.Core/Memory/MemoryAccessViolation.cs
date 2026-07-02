// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text;

namespace SharpEmu.Core.Memory;

/// <summary>Why a memory access was rejected.</summary>
public enum MemoryViolationReason
{
    /// <summary>No region owns the address.</summary>
    Unmapped,

    /// <summary>A region owns the address but the access spills past its end.</summary>
    OutOfBounds,

    /// <summary>The region exists but does not permit this kind of access.</summary>
    ProtectionDenied,

    /// <summary>The address falls inside a guard page.</summary>
    GuardPage,
}

/// <summary>
/// A structured description of an illegal guest memory access. Rendering it (see <see cref="Format"/>)
/// produces the diagnostic block the emulator surfaces instead of a bare host crash, e.g.:
/// <code>
/// MEMORY_ACCESS_VIOLATION
/// Address: 0x0000000123456789
/// Access: Write
/// Region: RX executable segment
/// Module: eboot.bin
/// Instruction pointer: 0x0000000100042210
/// </code>
/// </summary>
public sealed record MemoryAccessViolation(
    ulong Address,
    MemoryAccessKind Access,
    ulong Size,
    MemoryViolationReason Reason,
    MemoryRegionInfo? Region = null,
    string? ModuleName = null,
    ulong? InstructionPointer = null)
{
    /// <summary>Returns a copy enriched with the module and instruction pointer known only to the caller.</summary>
    public MemoryAccessViolation WithContext(string? moduleName, ulong? instructionPointer) =>
        this with { ModuleName = moduleName, InstructionPointer = instructionPointer };

    public string Format()
    {
        var region = Region is { } value ? value.DescribeRole() : "unmapped";
        var module = string.IsNullOrEmpty(ModuleName) ? "unknown" : ModuleName;
        var instructionPointer = InstructionPointer is { } ip ? $"0x{ip:X16}" : "unknown";

        var builder = new StringBuilder(160);
        builder.Append("MEMORY_ACCESS_VIOLATION").Append('\n');
        builder.Append("Address: 0x").AppendFormat("{0:X16}", Address).Append('\n');
        builder.Append("Access: ").Append(Access).Append('\n');
        builder.Append("Region: ").Append(region).Append('\n');
        builder.Append("Module: ").Append(module).Append('\n');
        builder.Append("Instruction pointer: ").Append(instructionPointer);
        return builder.ToString();
    }
}
