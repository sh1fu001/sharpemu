// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Libs.Agc;

/// <summary>The pipeline stage a bound Gen5 shader program drives.</summary>
internal enum ShaderStage
{
    /// <summary>Vertex/export (ES) shader.</summary>
    Vertex,

    /// <summary>Pixel/fragment (PS) shader.</summary>
    Pixel,

    /// <summary>Local/hull (LS) shader used with tessellation.</summary>
    Hull,

    /// <summary>Compute (CS) shader dispatched via ACB/DCB dispatches.</summary>
    Compute,
}

/// <summary>A shader program the guest bound through the SH registers: its stage and GPU address.</summary>
internal readonly record struct ShaderBinding(ShaderStage Stage, ulong Address);
