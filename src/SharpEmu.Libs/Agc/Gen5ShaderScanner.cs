// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers.Binary;

namespace SharpEmu.Libs.Agc;

/// <summary>
/// Minimal reader for Gen5 (GCN/RDNA-style) shader binaries. Phase 1 of the graphics pipeline does not
/// translate instructions yet; it only needs to find where a program ends (<c>s_endpgm</c>) so the code can be
/// bounded, dumped and fingerprinted for the shader metadata log.
/// </summary>
internal static class Gen5ShaderScanner
{
    /// <summary>The <c>s_endpgm</c> instruction word that terminates a shader program.</summary>
    public const uint SEndPgm = 0xBF810000u;

    /// <summary>Upper bound on how far the scanner will look for the terminator (64 KiB of code).</summary>
    public const int MaxProgramDwords = 16384;

    /// <summary>Finds the length in dwords up to and including the first <c>s_endpgm</c>.</summary>
    public static bool TryGetProgramDwordCount(ReadOnlySpan<byte> code, out int dwordCount)
    {
        dwordCount = 0;
        var total = Math.Min(code.Length / sizeof(uint), MaxProgramDwords);
        for (var index = 0; index < total; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(code.Slice(index * sizeof(uint), sizeof(uint))) == SEndPgm)
            {
                dwordCount = index + 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>Stable FNV-1a fingerprint of a shader program, used to dedupe and name dumps.</summary>
    public static ulong ComputeHash(ReadOnlySpan<byte> code)
    {
        const ulong fnvOffsetBasis = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        var hash = fnvOffsetBasis;
        foreach (var value in code)
        {
            hash = (hash ^ value) * fnvPrime;
        }

        return hash;
    }
}
