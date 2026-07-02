// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Core.Memory;

/// <summary>
/// A named block of backing storage that can be mapped at more than one address in a
/// <see cref="GuestAddressSpace"/>. Writes through any mapping are visible through the others, which is how
/// shared memory objects (e.g. cross-thread or GPU-visible buffers) are modelled.
/// </summary>
public sealed class SharedMemory
{
    internal SharedMemory(string name, byte[] data)
    {
        Name = name;
        Data = data;
    }

    public string Name { get; }

    public ulong Size => (ulong)Data.LongLength;

    internal byte[] Data { get; }
}
