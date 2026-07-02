// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Tests;

public sealed class ShaderTranslationPipelineTests
{
    private const uint OpFunction = 54;
    private const uint OpReturn = 253;

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

    private static void AssertValidSpirv(byte[] module)
    {
        Assert.True(module.Length >= 20 && module.Length % sizeof(uint) == 0);
        uint Word(int index) => BinaryPrimitives.ReadUInt32LittleEndian(module.AsSpan(index * sizeof(uint)));

        Assert.Equal(SpirvModule.MagicNumber, Word(0));
        var bound = Word(3);
        Assert.True(bound >= 1);

        var totalWords = module.Length / sizeof(uint);
        var index = 5; // skip the 5-word header
        var sawFunction = false;
        var sawReturn = false;
        while (index < totalWords)
        {
            var word = Word(index);
            var wordCount = (int)(word >> 16);
            var opcode = word & 0xFFFF;
            Assert.True(wordCount >= 1 && index + wordCount <= totalWords);
            sawFunction |= opcode == OpFunction;
            sawReturn |= opcode == OpReturn;
            index += wordCount;
        }

        Assert.Equal(totalWords, index); // instruction stream consumes exactly to the end
        Assert.True(sawFunction && sawReturn);
    }

    // SpirvExecutionModel is internal, so the public [Theory] takes its uint value and casts.
    [Theory]
    [InlineData((uint)SpirvExecutionModel.Vertex)]
    [InlineData((uint)SpirvExecutionModel.Fragment)]
    [InlineData((uint)SpirvExecutionModel.GLCompute)]
    public void EmitMinimalModule_ProducesStructurallyValidSpirv(uint model)
        => AssertValidSpirv(SpirvShaderEmitter.EmitMinimalModule((SpirvExecutionModel)model));

    [Fact]
    public void EncodeString_PacksAsciiLittleEndianWithTerminator()
    {
        var words = SpirvModule.EncodeString("main");

        Assert.Equal(2, words.Length);
        Assert.Equal(0x6E69616Du, words[0]); // 'm','a','i','n'
        Assert.Equal(0u, words[1]);           // NUL + padding
    }

    [Fact]
    public void Emit_PacksWordCountAndOpcode()
    {
        var module = new SpirvModule();
        module.Emit(17, 1); // OpCapability Shader

        var word = module.InstructionWords[0];
        Assert.Equal(2u, word >> 16);   // one opcode word + one operand
        Assert.Equal(17u, word & 0xFFFF);
    }

    [Fact]
    public void Cache_RoundTripsStoredSpirv()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sharpemu-shadercache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new ShaderTranslationCache(directory);
            var spirv = SpirvShaderEmitter.EmitMinimalModule(SpirvExecutionModel.Fragment);

            cache.Store(ShaderStage.Pixel, 0x1234, spirv);

            Assert.True(cache.TryLoad(ShaderStage.Pixel, 0x1234, out var loaded));
            Assert.Equal(spirv, loaded);
            Assert.False(cache.TryLoad(ShaderStage.Pixel, 0x9999, out _)); // unknown hash
            Assert.False(cache.TryLoad(ShaderStage.Vertex, 0x1234, out _)); // stage is part of the key
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Compiler_DecodesIr_EmitsSpirv_AndUsesCacheOnSecondCompile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sharpemu-shadercache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var code = ToBytes(PixelShader);
            var cache = new ShaderTranslationCache(directory);

            var first = Gen5ShaderCompiler.Compile(code, ShaderStage.Pixel, 0xABCD, cache);
            Assert.False(first.FromCache);
            Assert.Equal(4, first.InstructionCount); // VOP3, VOP3, EXP, s_endpgm
            AssertValidSpirv(first.Spirv);

            var second = Gen5ShaderCompiler.Compile(code, ShaderStage.Pixel, 0xABCD, cache);
            Assert.True(second.FromCache);
            Assert.Equal(first.Spirv, second.Spirv);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Compiler_WithoutCache_StillEmitsValidSpirv()
    {
        var result = Gen5ShaderCompiler.Compile(ToBytes(PixelShader), ShaderStage.Pixel, 0, cache: null);

        Assert.False(result.FromCache);
        Assert.Equal(4, result.InstructionCount);
        AssertValidSpirv(result.Spirv);
    }
}
