// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Tests;

public sealed class Gen5ShaderPipelineTests
{
    private static byte[] Program(params uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (var i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), words[i]);
        }

        return bytes;
    }

    [Fact]
    public void Scanner_FindsProgramLengthAtSEndPgm()
    {
        var code = Program(0x7E000000, Gen5ShaderScanner.SEndPgm, 0xDEADBEEF);

        Assert.True(Gen5ShaderScanner.TryGetProgramDwordCount(code, out var dwordCount));
        Assert.Equal(2, dwordCount); // includes s_endpgm, ignores trailing bytes
    }

    [Fact]
    public void Scanner_ReturnsFalseWhenNoTerminator()
    {
        var code = Program(0x7E000000, 0x7E000001);

        Assert.False(Gen5ShaderScanner.TryGetProgramDwordCount(code, out var dwordCount));
        Assert.Equal(0, dwordCount);
    }

    [Fact]
    public void Scanner_HashIsStableAndInputSensitive()
    {
        var a = Program(0x1, Gen5ShaderScanner.SEndPgm);
        var b = Program(0x2, Gen5ShaderScanner.SEndPgm);

        Assert.Equal(Gen5ShaderScanner.ComputeHash(a), Gen5ShaderScanner.ComputeHash(Program(0x1, Gen5ShaderScanner.SEndPgm)));
        Assert.NotEqual(Gen5ShaderScanner.ComputeHash(a), Gen5ShaderScanner.ComputeHash(b));
    }

    [Fact]
    public void Inspector_IdentifiesVertexAndPixelShaders()
    {
        var shRegisters = new Dictionary<uint, uint>
        {
            [0xC8] = 0x00040042, // SPI_SHADER_PGM_LO_ES
            [0xC9] = 0x00000001, // SPI_SHADER_PGM_HI_ES
            [0x8] = 0x00050000,  // SPI_SHADER_PGM_LO_PS
            [0x9] = 0x00000002,  // SPI_SHADER_PGM_HI_PS
        };

        var bindings = Gen5PipelineInspector.InspectGraphics(shRegisters);

        Assert.Equal(2, bindings.Count);
        var vertex = Assert.Single(bindings, binding => binding.Stage == ShaderStage.Vertex);
        Assert.Equal(Gen5PipelineInspector.ComposeAddress(0x00040042, 0x00000001), vertex.Address);
        Assert.Contains(bindings, binding => binding.Stage == ShaderStage.Pixel);
    }

    [Fact]
    public void Inspector_SkipsStageMissingHighRegister()
    {
        var shRegisters = new Dictionary<uint, uint> { [0x8] = 0x1000 }; // PS low only

        Assert.Empty(Gen5PipelineInspector.InspectGraphics(shRegisters));
    }

    [Fact]
    public void Inspector_DetectsComputeShader()
    {
        var shRegisters = new Dictionary<uint, uint>
        {
            [0x20C] = 0x00030000, // COMPUTE_PGM_LO
            [0x20D] = 0x00000001, // COMPUTE_PGM_HI
        };

        Assert.True(Gen5PipelineInspector.TryInspectCompute(shRegisters, out var binding));
        Assert.Equal(ShaderStage.Compute, binding.Stage);
        Assert.Equal(Gen5PipelineInspector.ComposeAddress(0x00030000, 0x00000001), binding.Address);
        Assert.False(Gen5PipelineInspector.TryInspectCompute(new Dictionary<uint, uint>(), out _));
    }
}
