// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using SharpEmu.Libs.VideoOut;
using Xunit;

namespace SharpEmu.Tests;

public sealed class Gen5ShaderTranslatorTests
{
    private const ulong EsAddress = 0x10000;
    private const ulong PsAddress = 0x11000;

    // The exact GCN bytecode the translator currently recognizes (fullscreen
    // barycentric export + pixel shader pair).
    private static readonly uint[] ExportShader =
    [
        0xBFA00001, 0x7E000000, 0x7E000000, 0x7E000000,
        0x93EBFF03, 0x00080008, 0x8F6A8C6B, 0x8700FF03,
        0x000000FF, 0x887C6A00, 0xBF900009, 0x81EA6BC0,
        0x90FE6AC1, 0xF8000941, 0x00000000, 0x81EA00C0,
        0xBF8CFF0F, 0x90FE6AC1, 0x36040A81, 0x2C060A81,
        0x7E000280, 0x7E0202F2, 0xD7460002, 0x03050302,
        0xD7460003, 0x03050303, 0x7E040B02, 0x7E060B03,
        0xF80008CF, 0x01000302, 0xBF810000,
    ];

    private static readonly uint[] PixelShader =
    [
        0xD52F0000, 0x00000200,
        0xD52F0001, 0x00000602,
        0xF8001C0F, 0x00000100,
        0xBF810000,
    ];

    private static byte[] ToBytes(uint[] words)
    {
        var bytes = new byte[words.Length * sizeof(uint)];
        for (var i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), words[i]);
        }

        return bytes;
    }

    private static CpuContext CreateContext(uint[] es, uint[] ps)
    {
        var memory = new VirtualMemory();
        var rx = ProgramHeaderFlags.Read | ProgramHeaderFlags.Execute;
        memory.Map(EsAddress, 0x200, 0, ToBytes(es), rx);
        memory.Map(PsAddress, 0x100, 0, ToBytes(ps), rx);
        return new CpuContext(memory, Generation.Gen5);
    }

    [Fact]
    public void TryTranslate_KnownShaderPair_ReturnsFullscreenBarycentric()
    {
        var ctx = CreateContext(ExportShader, PixelShader);

        var result = Gen5ShaderTranslator.TryTranslate(ctx, EsAddress, PsAddress, 0x2, 0x2, out var drawKind);

        Assert.True(result);
        Assert.Equal(GuestDrawKind.FullscreenBarycentric, drawKind);
    }

    [Fact]
    public void TryTranslate_MismatchedExportBytecode_ReturnsFalse()
    {
        var tampered = (uint[])ExportShader.Clone();
        tampered[10] ^= 0xFFFFFFFF; // corrupt one instruction word
        var ctx = CreateContext(tampered, PixelShader);

        var result = Gen5ShaderTranslator.TryTranslate(ctx, EsAddress, PsAddress, 0x2, 0x2, out var drawKind);

        Assert.False(result);
        Assert.Equal(GuestDrawKind.None, drawKind);
    }

    [Fact]
    public void TryTranslate_WrongPsInputFlags_ReturnsFalse()
    {
        var ctx = CreateContext(ExportShader, PixelShader);

        Assert.False(Gen5ShaderTranslator.TryTranslate(ctx, EsAddress, PsAddress, 0x1, 0x2, out var k1));
        Assert.Equal(GuestDrawKind.None, k1);

        Assert.False(Gen5ShaderTranslator.TryTranslate(ctx, EsAddress, PsAddress, 0x2, 0x99, out var k2));
        Assert.Equal(GuestDrawKind.None, k2);
    }

    [Fact]
    public void TryTranslate_NullAddresses_ReturnsFalse()
    {
        var ctx = CreateContext(ExportShader, PixelShader);

        Assert.False(Gen5ShaderTranslator.TryTranslate(ctx, 0, PsAddress, 0x2, 0x2, out var drawKind));
        Assert.Equal(GuestDrawKind.None, drawKind);
    }
}
