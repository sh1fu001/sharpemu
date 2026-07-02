// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace SharpEmu.Libs.Agc;

/// <summary>
/// Identifies which shader programs a submitted command buffer has bound, purely from the captured SH
/// register state. This is the "identify vertex/pixel/compute shader" step of the graphics logging phase and
/// is deliberately free of any <c>CpuContext</c> dependency so it can be unit-tested in isolation.
/// </summary>
internal static class Gen5PipelineInspector
{
    private const uint SpiShaderPgmLoPs = 0x8;
    private const uint SpiShaderPgmHiPs = 0x9;
    private const uint SpiShaderPgmLoEs = 0xC8;
    private const uint SpiShaderPgmHiEs = 0xC9;
    private const uint SpiShaderPgmLoLs = 0x148;
    private const uint SpiShaderPgmHiLs = 0x149;
    private const uint ComputePgmLo = 0x20C;
    private const uint ComputePgmHi = 0x20D;

    private static readonly (ShaderStage Stage, uint Lo, uint Hi)[] GraphicsStages =
    [
        (ShaderStage.Vertex, SpiShaderPgmLoEs, SpiShaderPgmHiEs),
        (ShaderStage.Pixel, SpiShaderPgmLoPs, SpiShaderPgmHiPs),
        (ShaderStage.Hull, SpiShaderPgmLoLs, SpiShaderPgmHiLs),
    ];

    /// <summary>Returns the graphics shader stages (vertex/pixel/hull) currently bound by the SH registers.</summary>
    public static IReadOnlyList<ShaderBinding> InspectGraphics(IReadOnlyDictionary<uint, uint> shRegisters)
    {
        ArgumentNullException.ThrowIfNull(shRegisters);
        var bindings = new List<ShaderBinding>(GraphicsStages.Length);
        foreach (var (stage, lo, hi) in GraphicsStages)
        {
            if (TryGetShaderAddress(shRegisters, lo, hi, out var address))
            {
                bindings.Add(new ShaderBinding(stage, address));
            }
        }

        return bindings;
    }

    /// <summary>Returns the compute shader bound by the SH registers, if any.</summary>
    public static bool TryInspectCompute(IReadOnlyDictionary<uint, uint> shRegisters, out ShaderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(shRegisters);
        if (TryGetShaderAddress(shRegisters, ComputePgmLo, ComputePgmHi, out var address))
        {
            binding = new ShaderBinding(ShaderStage.Compute, address);
            return true;
        }

        binding = default;
        return false;
    }

    /// <summary>Reconstructs a shader GPU address from its low/high SH register pair.</summary>
    public static ulong ComposeAddress(uint lo, uint hi) => ((ulong)hi << 40) | ((ulong)lo << 8);

    private static bool TryGetShaderAddress(
        IReadOnlyDictionary<uint, uint> registers,
        uint loRegister,
        uint hiRegister,
        out ulong address)
    {
        address = 0;
        if (!registers.TryGetValue(loRegister, out var lo) || !registers.TryGetValue(hiRegister, out var hi))
        {
            return false;
        }

        address = ComposeAddress(lo, hi);
        return address != 0;
    }
}
