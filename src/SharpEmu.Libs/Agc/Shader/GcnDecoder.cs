// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace SharpEmu.Libs.Agc;

/// <summary>
/// Decodes GCN/Gen5 shader microcode into <see cref="GcnInstruction"/> IR. It classifies each word by its
/// encoding prefix, works out the instruction length (including the trailing 32-bit literal some encodings
/// carry), and extracts the opcode. This is phase 2's "AGC/Gen5 -&gt; IR" front end; instruction semantics are
/// lowered to SPIR-V by later stages.
/// </summary>
internal static class GcnDecoder
{
    /// <summary>SOPP opcode for <c>s_endpgm</c>.</summary>
    public const uint SoppEndPgm = 1;

    private const uint LiteralOperand = 255;

    /// <summary>Decodes a single instruction from the front of <paramref name="words"/>.</summary>
    public static bool TryDecode(ReadOnlySpan<uint> words, out GcnInstruction instruction)
    {
        instruction = default;
        if (words.IsEmpty)
        {
            return false;
        }

        var word0 = words[0];
        var encoding = Classify(word0);
        var opcode = ExtractOpcode(encoding, word0);
        var length = IsDoubleWord(encoding) ? 2 : 1;

        var hasLiteral = length == 1 && SupportsLiteral(encoding) && UsesLiteral(encoding, word0);
        if (hasLiteral)
        {
            length = 2;
        }

        if (words.Length < length)
        {
            return false;
        }

        var word1 = length == 2 ? words[1] : 0u;
        instruction = new GcnInstruction(encoding, opcode, length, word0, word1, hasLiteral, hasLiteral ? word1 : 0u);
        return true;
    }

    /// <summary>Decodes a whole program, stopping at (and including) the terminating <c>s_endpgm</c>.</summary>
    public static IReadOnlyList<GcnInstruction> DecodeProgram(ReadOnlySpan<byte> code)
    {
        var dwordCount = code.Length / sizeof(uint);
        var words = new uint[dwordCount];
        for (var i = 0; i < dwordCount; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(code.Slice(i * sizeof(uint), sizeof(uint)));
        }

        var instructions = new List<GcnInstruction>();
        var offset = 0;
        while (offset < words.Length)
        {
            if (!TryDecode(words.AsSpan(offset), out var instruction))
            {
                break;
            }

            instructions.Add(instruction);
            offset += instruction.Length;
            if (instruction.IsEndOfProgram)
            {
                break;
            }
        }

        return instructions;
    }

    public static GcnEncoding Classify(uint word0)
    {
        // Scalar special forms live inside the 10xxxxxx space and must be matched before the generic SOP2/SOPK.
        if ((word0 & 0xFF800000) == 0xBF800000)
        {
            return GcnEncoding.Sopp;
        }

        if ((word0 & 0xFF800000) == 0xBF000000)
        {
            return GcnEncoding.Sopc;
        }

        if ((word0 & 0xFF800000) == 0xBE800000)
        {
            return GcnEncoding.Sop1;
        }

        if ((word0 & 0xF0000000) == 0xB0000000)
        {
            return GcnEncoding.Sopk;
        }

        if ((word0 & 0xC0000000) == 0x80000000)
        {
            return GcnEncoding.Sop2;
        }

        // 11xxxxxx: scalar memory, vector ALU (VOP3), data share, buffer/image, export.
        if ((word0 & 0xFC000000) == 0xC0000000)
        {
            return GcnEncoding.Smem;
        }

        if ((word0 & 0xFC000000) == 0xC8000000)
        {
            return GcnEncoding.Vintrp;
        }

        if ((word0 & 0xF8000000) == 0xD0000000)
        {
            return GcnEncoding.Vop3;
        }

        if ((word0 & 0xFC000000) == 0xD8000000)
        {
            return GcnEncoding.Ds;
        }

        if ((word0 & 0xFC000000) == 0xDC000000)
        {
            return GcnEncoding.Flat;
        }

        if ((word0 & 0xFC000000) == 0xE0000000)
        {
            return GcnEncoding.Mubuf;
        }

        if ((word0 & 0xFC000000) == 0xE8000000)
        {
            return GcnEncoding.Mtbuf;
        }

        if ((word0 & 0xFC000000) == 0xF0000000)
        {
            return GcnEncoding.Mimg;
        }

        if ((word0 & 0xFC000000) == 0xF8000000)
        {
            return GcnEncoding.Exp;
        }

        // 0xxxxxxx: vector ALU. VOP1/VOPC carve ranges out of the VOP2 space.
        if ((word0 & 0xFE000000) == 0x7E000000)
        {
            return GcnEncoding.Vop1;
        }

        if ((word0 & 0xFE000000) == 0x7C000000)
        {
            return GcnEncoding.Vopc;
        }

        if ((word0 & 0x80000000) == 0)
        {
            return GcnEncoding.Vop2;
        }

        return GcnEncoding.Unknown;
    }

    private static bool IsDoubleWord(GcnEncoding encoding) => encoding is
        GcnEncoding.Smem or
        GcnEncoding.Vop3 or
        GcnEncoding.Ds or
        GcnEncoding.Flat or
        GcnEncoding.Mubuf or
        GcnEncoding.Mtbuf or
        GcnEncoding.Mimg or
        GcnEncoding.Exp;

    private static bool SupportsLiteral(GcnEncoding encoding) => encoding is
        GcnEncoding.Sop2 or
        GcnEncoding.Sop1 or
        GcnEncoding.Sopc or
        GcnEncoding.Vop1 or
        GcnEncoding.Vop2 or
        GcnEncoding.Vopc;

    private static bool UsesLiteral(GcnEncoding encoding, uint word0) => encoding switch
    {
        GcnEncoding.Sop2 or GcnEncoding.Sopc => (word0 & 0xFF) == LiteralOperand || ((word0 >> 8) & 0xFF) == LiteralOperand,
        GcnEncoding.Sop1 => (word0 & 0xFF) == LiteralOperand,
        GcnEncoding.Vop1 or GcnEncoding.Vop2 or GcnEncoding.Vopc => (word0 & 0x1FF) == LiteralOperand,
        _ => false,
    };

    private static uint ExtractOpcode(GcnEncoding encoding, uint word0) => encoding switch
    {
        GcnEncoding.Sop2 => (word0 >> 23) & 0x7F,
        GcnEncoding.Sopk => (word0 >> 23) & 0x1F,
        GcnEncoding.Sop1 => (word0 >> 8) & 0xFF,
        GcnEncoding.Sopc => (word0 >> 16) & 0x7F,
        GcnEncoding.Sopp => (word0 >> 16) & 0x7F,
        GcnEncoding.Smem => (word0 >> 18) & 0xFF,
        GcnEncoding.Vop1 => (word0 >> 9) & 0xFF,
        GcnEncoding.Vop2 => (word0 >> 25) & 0x3F,
        GcnEncoding.Vopc => (word0 >> 17) & 0xFF,
        GcnEncoding.Vop3 => (word0 >> 16) & 0x3FF,
        GcnEncoding.Vintrp => (word0 >> 16) & 0x3,
        _ => 0,
    };
}
