// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.HLE;
using SharpEmu.Libs.Pad;
using Silk.NET.Input;
using System.Buffers.Binary;
using Xunit;

namespace SharpEmu.Tests;

public sealed class PadInputTests
{
    private const ulong DataAddress = 0x10000;

    [Fact]
    public void ReadGamepadButtons_MapsStandardLayoutToPlayStationButtons()
    {
        Button[] buttons =
        [
            new(ButtonName.A, 0, true),
            new(ButtonName.B, 1, true),
            new(ButtonName.X, 2, true),
            new(ButtonName.Y, 3, true),
            new(ButtonName.LeftBumper, 4, true),
            new(ButtonName.RightBumper, 5, true),
            new(ButtonName.Back, 6, true),
            new(ButtonName.Start, 7, true),
            new(ButtonName.LeftStick, 8, true),
            new(ButtonName.RightStick, 9, true),
            new(ButtonName.DPadUp, 10, true),
            new(ButtonName.DPadRight, 11, true),
            new(ButtonName.DPadDown, 12, true),
            new(ButtonName.DPadLeft, 13, true),
        ];

        var mapped = PadStateMapper.ReadGamepadButtons(buttons);

        var expected =
            PadButtonMasks.Cross |
            PadButtonMasks.Circle |
            PadButtonMasks.Square |
            PadButtonMasks.Triangle |
            PadButtonMasks.L1 |
            PadButtonMasks.R1 |
            PadButtonMasks.TouchPad |
            PadButtonMasks.Options |
            PadButtonMasks.L3 |
            PadButtonMasks.R3 |
            PadButtonMasks.Up |
            PadButtonMasks.Right |
            PadButtonMasks.Down |
            PadButtonMasks.Left;
        Assert.Equal(expected, mapped);
    }

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(0f, 128)]
    [InlineData(1f, 255)]
    [InlineData(-2f, 0)]
    [InlineData(2f, 255)]
    public void NormalizeStick_MapsSignedAxisToOrbisByte(float input, byte expected) =>
        Assert.Equal(expected, PadStateMapper.NormalizeStick(input));

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(0f, 128)]
    [InlineData(1f, 255)]
    public void NormalizeGlfwTrigger_MapsReleasedToPressedRange(float input, byte expected) =>
        Assert.Equal(expected, PadStateMapper.NormalizeGlfwTrigger(input));

    [Fact]
    public void ApplyRadialDeadzone_CentersNoiseAndPreservesFullScale()
    {
        var noiseX = 0.05f;
        var noiseY = -0.04f;
        PadStateMapper.ApplyRadialDeadzone(ref noiseX, ref noiseY, 0.12f);
        Assert.Equal(0, noiseX);
        Assert.Equal(0, noiseY);

        var fullX = 1f;
        var fullY = 0f;
        PadStateMapper.ApplyRadialDeadzone(ref fullX, ref fullY, 0.12f);
        Assert.Equal(1f, fullX, precision: 5);
        Assert.Equal(0f, fullY);
    }

    [Fact]
    public void HostPadStateCache_RoundTripsCompleteSnapshot()
    {
        var expected = new HostPadState(
            0x0010C80E,
            1,
            2,
            253,
            254,
            127,
            255,
            true);

        HostPadStateCache.Publish(expected);

        Assert.Equal(expected, HostPadStateCache.Read());
        HostPadStateCache.Reset();
        Assert.Equal(HostPadState.Neutral, HostPadStateCache.Read());
    }

    [Fact]
    public void PadReadState_WritesButtonsSticksAndTriggersAtOrbisOffsets()
    {
        var memory = CreateMemory();
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = DataAddress;
        var state = new HostPadState(
            PadButtonMasks.Cross | PadButtonMasks.L1,
            10,
            20,
            30,
            40,
            50,
            60,
            true);
        HostPadStateCache.Publish(state);

        try
        {
            Assert.Equal(0, PadExports.PadReadState(context));

            Span<byte> data = stackalloc byte[0x78];
            Assert.True(memory.TryRead(DataAddress, data));
            Assert.Equal(state.Buttons, BinaryPrimitives.ReadUInt32LittleEndian(data));
            Assert.Equal(10, data[0x04]);
            Assert.Equal(20, data[0x05]);
            Assert.Equal(30, data[0x06]);
            Assert.Equal(40, data[0x07]);
            Assert.Equal(50, data[0x08]);
            Assert.Equal(60, data[0x09]);
            Assert.Equal(1, data[0x4C]);
        }
        finally
        {
            HostPadStateCache.Reset();
        }
    }

    [Fact]
    public void PadSetVibration_QueuesBothMotorIntensities()
    {
        var memory = CreateMemory();
        Assert.True(memory.TryWrite(DataAddress, [200, 75]));
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = DataAddress;

        Assert.Equal(0, PadExports.PadSetVibration(context));
        Assert.Equal(((byte)200, (byte)75), HostPadOutputCache.ReadVibration());

        HostPadOutputCache.SetVibration(0, 0);
    }

    private static VirtualMemory CreateMemory()
    {
        var memory = new VirtualMemory();
        memory.Map(
            DataAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);
        return memory;
    }
}
