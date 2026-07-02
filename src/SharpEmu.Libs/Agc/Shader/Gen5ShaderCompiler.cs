// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace SharpEmu.Libs.Agc;

/// <summary>The result of compiling a guest shader: the SPIR-V bytes plus how they were produced.</summary>
internal readonly record struct CompiledShader(byte[] Spirv, int InstructionCount, bool FromCache);

/// <summary>
/// Phase 2 translation pipeline: decode GCN/Gen5 microcode to IR, then emit SPIR-V, using an optional on-disk
/// <see cref="ShaderTranslationCache"/> to skip re-translation. The IR decode is real; the SPIR-V it emits is
/// currently a valid minimal module (per-instruction lowering is the next increment), so the whole path —
/// decode, cache, valid SPIR-V bytes — can be exercised end to end today.
/// </summary>
internal static class Gen5ShaderCompiler
{
    public static CompiledShader Compile(
        ReadOnlySpan<byte> code,
        ShaderStage stage,
        ulong hash,
        ShaderTranslationCache? cache = null)
    {
        if (cache is not null && hash != 0 && cache.TryLoad(stage, hash, out var cached))
        {
            return new CompiledShader(cached, InstructionCount: -1, FromCache: true);
        }

        var instructions = GcnDecoder.DecodeProgram(code);
        var spirv = SpirvShaderEmitter.EmitMinimalModule(ToExecutionModel(stage));

        if (cache is not null && hash != 0)
        {
            cache.Store(stage, hash, spirv);
        }

        return new CompiledShader(spirv, instructions.Count, FromCache: false);
    }

    private static SpirvExecutionModel ToExecutionModel(ShaderStage stage) => stage switch
    {
        ShaderStage.Pixel => SpirvExecutionModel.Fragment,
        ShaderStage.Compute => SpirvExecutionModel.GLCompute,
        _ => SpirvExecutionModel.Vertex,
    };
}
