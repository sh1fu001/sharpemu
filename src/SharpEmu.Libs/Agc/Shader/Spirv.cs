// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace SharpEmu.Libs.Agc;

/// <summary>SPIR-V execution model for the entry point being emitted.</summary>
internal enum SpirvExecutionModel : uint
{
    Vertex = 0,
    Fragment = 4,
    GLCompute = 5,
}

/// <summary>
/// A tiny SPIR-V binary assembler: it appends instruction words and produces a valid module header. It is the
/// "IR -&gt; SPIR-V" back end's foundation — the piece that guarantees byte-correct SPIR-V — onto which
/// per-instruction lowering is built.
/// </summary>
internal sealed class SpirvModule
{
    public const uint MagicNumber = 0x07230203;
    private const uint Version = 0x00010300; // SPIR-V 1.3
    private const uint GeneratorMagic = 0x0000_2A00; // informational tool id

    private readonly List<uint> _instructionWords = new();
    private uint _bound = 1; // id 0 is reserved

    public uint Bound => _bound;

    public IReadOnlyList<uint> InstructionWords => _instructionWords;

    /// <summary>Reserves and returns a fresh result id.</summary>
    public uint AllocateId() => _bound++;

    /// <summary>Appends one instruction: the leading word packs word count and opcode.</summary>
    public void Emit(uint opcode, params uint[] operands)
    {
        var wordCount = (uint)(operands.Length + 1);
        _instructionWords.Add((wordCount << 16) | (opcode & 0xFFFF));
        _instructionWords.AddRange(operands);
    }

    /// <summary>Encodes an ASCII string as NUL-terminated, 4-byte-padded SPIR-V literal words.</summary>
    public static uint[] EncodeString(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var wordCount = (bytes.Length / sizeof(uint)) + 1; // always room for the terminator + padding
        var words = new uint[wordCount];
        for (var i = 0; i < bytes.Length; i++)
        {
            words[i / sizeof(uint)] |= (uint)bytes[i] << (8 * (i % sizeof(uint)));
        }

        return words;
    }

    public byte[] ToBytes()
    {
        var module = new List<uint>(5 + _instructionWords.Count)
        {
            MagicNumber,
            Version,
            GeneratorMagic,
            _bound,
            0, // reserved schema
        };
        module.AddRange(_instructionWords);

        var bytes = new byte[module.Count * sizeof(uint)];
        for (var i = 0; i < module.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), module[i]);
        }

        return bytes;
    }
}

/// <summary>
/// Emits minimal but structurally valid SPIR-V modules. Instruction-level lowering from GCN IR is the next
/// step; today this produces a well-formed empty entry point so the rest of the pipeline (cache, Vulkan)
/// can be exercised against real SPIR-V bytes.
/// </summary>
internal static class SpirvShaderEmitter
{
    private const uint OpMemoryModel = 14;
    private const uint OpEntryPoint = 15;
    private const uint OpExecutionMode = 16;
    private const uint OpCapability = 17;
    private const uint OpTypeVoid = 19;
    private const uint OpTypeFunction = 33;
    private const uint OpFunction = 54;
    private const uint OpFunctionEnd = 56;
    private const uint OpLabel = 248;
    private const uint OpReturn = 253;

    private const uint CapabilityShader = 1;
    private const uint AddressingModelLogical = 0;
    private const uint MemoryModelGlsl450 = 1;
    private const uint FunctionControlNone = 0;
    private const uint ExecutionModeOriginUpperLeft = 7;
    private const uint ExecutionModeLocalSize = 17;

    public static byte[] EmitMinimalModule(SpirvExecutionModel model)
    {
        var spirv = new SpirvModule();
        var mainId = spirv.AllocateId();
        var voidTypeId = spirv.AllocateId();
        var functionTypeId = spirv.AllocateId();
        var labelId = spirv.AllocateId();

        spirv.Emit(OpCapability, CapabilityShader);
        spirv.Emit(OpMemoryModel, AddressingModelLogical, MemoryModelGlsl450);

        var entryPoint = new List<uint> { (uint)model, mainId };
        entryPoint.AddRange(SpirvModule.EncodeString("main"));
        spirv.Emit(OpEntryPoint, entryPoint.ToArray());

        switch (model)
        {
            case SpirvExecutionModel.Fragment:
                spirv.Emit(OpExecutionMode, mainId, ExecutionModeOriginUpperLeft);
                break;
            case SpirvExecutionModel.GLCompute:
                spirv.Emit(OpExecutionMode, mainId, ExecutionModeLocalSize, 1, 1, 1);
                break;
        }

        spirv.Emit(OpTypeVoid, voidTypeId);
        spirv.Emit(OpTypeFunction, functionTypeId, voidTypeId);
        spirv.Emit(OpFunction, voidTypeId, mainId, FunctionControlNone, functionTypeId);
        spirv.Emit(OpLabel, labelId);
        spirv.Emit(OpReturn);
        spirv.Emit(OpFunctionEnd);

        return spirv.ToBytes();
    }
}
