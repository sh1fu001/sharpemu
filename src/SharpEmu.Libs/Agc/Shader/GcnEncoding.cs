// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Libs.Agc;

/// <summary>
/// The GCN/Gen5 instruction encoding family a decoded word belongs to. The family determines the
/// instruction length (32- vs 64-bit) and how the opcode and operands are laid out.
/// </summary>
internal enum GcnEncoding
{
    Unknown,

    // Scalar ALU / control.
    Sop2,
    Sopk,
    Sop1,
    Sopc,
    Sopp,

    // Scalar memory.
    Smem,

    // Vector ALU.
    Vop1,
    Vop2,
    Vopc,
    Vop3,
    Vintrp,

    // Data share / memory / export.
    Ds,
    Flat,
    Mubuf,
    Mtbuf,
    Mimg,
    Exp,
}
