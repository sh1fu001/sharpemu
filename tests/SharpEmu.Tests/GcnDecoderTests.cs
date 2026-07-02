// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Tests;

public sealed class GcnDecoderTests
{
    // The pixel shader of the fullscreen barycentric program: VOP3, VOP3, EXP, s_endpgm.
    private static readonly uint[] PixelShader =
    [
        0xD52F0000, 0x00000200,
        0xD52F0001, 0x00000602,
        0xF8001C0F, 0x00000100,
        0xBF810000,
    ];

    private static byte[] ToBytes(params uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (var i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), words[i]);
        }

        return bytes;
    }

    // The enum is internal, so a public [Theory] cannot take it directly; pass the int value and cast.
    [Theory]
    [InlineData(0xBF810000u, (int)GcnEncoding.Sopp, 1)] // s_endpgm
    [InlineData(0xBF800000u, (int)GcnEncoding.Sopp, 1)] // s_nop
    [InlineData(0xD52F0000u, (int)GcnEncoding.Vop3, 2)] // VOP3 (always 64-bit)
    [InlineData(0xF8001C0Fu, (int)GcnEncoding.Exp, 2)]  // EXP (always 64-bit)
    [InlineData(0x7E000000u, (int)GcnEncoding.Vop1, 1)] // v_mov-style, no literal
    [InlineData(0x2C060A81u, (int)GcnEncoding.Vop2, 1)] // VOP2, no literal
    [InlineData(0xBE800000u, (int)GcnEncoding.Sop1, 1)] // SOP1, no literal
    public void TryDecode_ClassifiesEncodingAndLength(uint word, int expectedEncoding, int expectedLength)
    {
        Assert.True(GcnDecoder.TryDecode(new[] { word, 0u }, out var instruction));
        Assert.Equal(expectedEncoding, (int)instruction.Encoding);
        Assert.Equal(expectedLength, instruction.Length);
    }

    [Fact]
    public void TryDecode_SEndPgm_IsRecognisedAsEndOfProgram()
    {
        Assert.True(GcnDecoder.TryDecode(new[] { 0xBF810000u }, out var instruction));
        Assert.Equal(GcnEncoding.Sopp, instruction.Encoding);
        Assert.Equal(GcnDecoder.SoppEndPgm, instruction.Opcode);
        Assert.True(instruction.IsEndOfProgram);
    }

    [Fact]
    public void TryDecode_Sop2WithLiteralSource_ConsumesTrailingLiteral()
    {
        // 0x93EBFF03: SOP2 whose ssrc1 == 255 (LITERAL), so a 32-bit constant follows.
        Assert.True(GcnDecoder.TryDecode(new[] { 0x93EBFF03u, 0x00080008u }, out var instruction));
        Assert.Equal(GcnEncoding.Sop2, instruction.Encoding);
        Assert.Equal(2, instruction.Length);
        Assert.True(instruction.HasLiteral);
        Assert.Equal(0x00080008u, instruction.Literal);
    }

    [Fact]
    public void DecodeProgram_WalksPixelShaderToEndPgm()
    {
        var instructions = GcnDecoder.DecodeProgram(ToBytes(PixelShader));

        Assert.Equal(4, instructions.Count);
        Assert.Equal(GcnEncoding.Vop3, instructions[0].Encoding);
        Assert.Equal(GcnEncoding.Vop3, instructions[1].Encoding);
        Assert.Equal(GcnEncoding.Exp, instructions[2].Encoding);
        Assert.True(instructions[3].IsEndOfProgram);
    }

    [Fact]
    public void DecodeProgram_StopsAtEndPgm_IgnoringTrailingBytes()
    {
        var instructions = GcnDecoder.DecodeProgram(ToBytes(0xBF810000u, 0xDEADBEEFu));

        Assert.Single(instructions);
        Assert.True(instructions[0].IsEndOfProgram);
    }
}
