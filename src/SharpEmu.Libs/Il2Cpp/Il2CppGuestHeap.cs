// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpEmu.HLE;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Guest-address-space heap backing every il2cpp block handed to the game (class blocks, strings,
/// arrays, objects, method/field infos, opaque handles, il2cpp_alloc buffers). These previously came
/// from the host allocator; direct execution makes host pointers dereferenceable by guest code, but
/// they are invisible to the emulator's bounds-checked guest memory interface, so every HLE path
/// handed such a pointer back (kernel I/O on string chars, pointer-window dumps, guest memory
/// queries) rejected it. Sub-allocates 16-byte-aligned blocks out of chunks acquired from the
/// emulator's guest allocation arena, with exact-size free lists so il2cpp_free can recycle blocks
/// (the arena itself is bump-only and never releases).
/// </summary>
public sealed class Il2CppGuestHeap
{
    private const ulong ChunkSize = 0x20_0000; // 2 MiB
    private const ulong Alignment = 0x10;      // il2cpp_allocation_granularity

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");

    private readonly object _gate = new();
    private readonly IGuestMemoryAllocator _allocator;
    private readonly Dictionary<ulong, ulong> _blockSizes = new();
    private readonly Dictionary<ulong, Stack<ulong>> _freeBlocksBySize = new();
    private ulong _cursor;
    private ulong _cursorEnd;
    private ulong _allocatedBytes;
    private bool _exhaustionLogged;

    public Il2CppGuestHeap(IGuestMemoryAllocator allocator)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    }

    /// <summary>Bytes currently handed out to live blocks (diagnostics).</summary>
    public ulong AllocatedBytes
    {
        get
        {
            lock (_gate)
            {
                return _allocatedBytes;
            }
        }
    }

    /// <summary>
    /// Allocates a zeroed, 16-byte-aligned block in guest address space. Returns 0 when the guest
    /// arena cannot satisfy the request (callers translate that to a NULL for the guest).
    /// </summary>
    public ulong AllocateZeroed(ulong size)
    {
        if (size == 0)
        {
            size = Alignment;
        }

        if (size > ulong.MaxValue - Alignment)
        {
            return 0;
        }

        var alignedSize = (size + (Alignment - 1)) & ~(Alignment - 1);
        lock (_gate)
        {
            if (_freeBlocksBySize.TryGetValue(alignedSize, out var freeList) && freeList.Count > 0)
            {
                var recycled = freeList.Pop();
                ZeroBlock(recycled, alignedSize);
                _blockSizes[recycled] = alignedSize;
                _allocatedBytes += alignedSize;
                return recycled;
            }

            ulong address;
            if (alignedSize > ChunkSize)
            {
                // Oversized request: dedicated allocation, no sub-allocation. Fresh pages are zero.
                if (!_allocator.TryAllocateGuestMemory(alignedSize, Alignment, out address))
                {
                    LogExhaustionLocked(alignedSize);
                    return 0;
                }
            }
            else
            {
                if (_cursorEnd - _cursor < alignedSize && !TryAcquireChunkLocked())
                {
                    LogExhaustionLocked(alignedSize);
                    return 0;
                }

                address = _cursor;
                _cursor += alignedSize;
            }

            _blockSizes[address] = alignedSize;
            _allocatedBytes += alignedSize;
            return address;
        }
    }

    /// <summary>
    /// Recycles a block previously returned by <see cref="AllocateZeroed"/>. Returns false when the
    /// pointer does not belong to this heap (caller decides how to free it).
    /// </summary>
    public bool Free(ulong address)
    {
        lock (_gate)
        {
            if (!_blockSizes.Remove(address, out var size))
            {
                return false;
            }

            if (!_freeBlocksBySize.TryGetValue(size, out var freeList))
            {
                _freeBlocksBySize[size] = freeList = new Stack<ulong>();
            }

            freeList.Push(address);
            _allocatedBytes -= size;
            return true;
        }
    }

    private bool TryAcquireChunkLocked()
    {
        if (!_allocator.TryAllocateGuestMemory(ChunkSize, Alignment, out var chunkBase))
        {
            return false;
        }

        // The tail of the previous chunk (< one block) is abandoned; the arena is bump-only anyway.
        _cursor = chunkBase;
        _cursorEnd = chunkBase + ChunkSize;
        return true;
    }

    private void LogExhaustionLocked(ulong size)
    {
        if (_exhaustionLogged)
        {
            return;
        }

        _exhaustionLogged = true;
        Log.Error(
            $"Guest allocation arena cannot satisfy an il2cpp block of {size} bytes " +
            $"(live heap bytes: {_allocatedBytes}); returning NULL to the guest.");
    }

    private static unsafe void ZeroBlock(ulong address, ulong size)
    {
        // Direct execution maps guest VA == host VA, so the block is addressable in-process.
        NativeMemory.Clear((void*)address, (nuint)size);
    }
}
