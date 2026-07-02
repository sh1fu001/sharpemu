// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Libs.Agc;

/// <summary>
/// One decoded GCN/Gen5 instruction: the intermediate representation the shader translator works on. It keeps
/// the raw dword(s) alongside the decoded encoding/opcode/length so later phases can lower it to SPIR-V
/// without re-parsing.
/// </summary>
internal readonly record struct GcnInstruction(
    GcnEncoding Encoding,
    uint Opcode,
    int Length,
    uint Word0,
    uint Word1,
    bool HasLiteral,
    uint Literal)
{
    /// <summary>True for <c>s_endpgm</c>, which terminates a shader program.</summary>
    public bool IsEndOfProgram => Encoding == GcnEncoding.Sopp && Opcode == GcnDecoder.SoppEndPgm;
}
