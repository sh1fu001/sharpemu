// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;

namespace SharpEmu.Libs.Pad;

internal static class DualSenseHidReport
{
    internal const ushort SonyVendorId = 0x054C;
    internal const ushort DualSenseProductId = 0x0CE6;
    internal const ushort DualSenseEdgeProductId = 0x0DF2;
    internal const int UsbInputLength = 64;
    internal const int UsbOutputLength = 48;

    internal static bool IsSupported(ushort vendorId, ushort productId) =>
        vendorId == SonyVendorId &&
        (productId == DualSenseProductId || productId == DualSenseEdgeProductId);

    internal static bool TryParse(
        ReadOnlySpan<byte> report,
        float deadzone,
        out HostPadState state,
        out HostPadMotionState motion)
    {
        state = HostPadState.Neutral;
        motion = HostPadMotionState.Neutral;
        if (report.Length < UsbInputLength || report[0] != 0x01)
        {
            return false;
        }

        var buttons = 0u;
        buttons |= (report[8] & 0x0F) switch
        {
            0 => PadButtonMasks.Up,
            1 => PadButtonMasks.Up | PadButtonMasks.Right,
            2 => PadButtonMasks.Right,
            3 => PadButtonMasks.Right | PadButtonMasks.Down,
            4 => PadButtonMasks.Down,
            5 => PadButtonMasks.Down | PadButtonMasks.Left,
            6 => PadButtonMasks.Left,
            7 => PadButtonMasks.Left | PadButtonMasks.Up,
            _ => 0,
        };
        if ((report[8] & 0x10) != 0) buttons |= PadButtonMasks.Square;
        if ((report[8] & 0x20) != 0) buttons |= PadButtonMasks.Cross;
        if ((report[8] & 0x40) != 0) buttons |= PadButtonMasks.Circle;
        if ((report[8] & 0x80) != 0) buttons |= PadButtonMasks.Triangle;
        if ((report[9] & 0x01) != 0) buttons |= PadButtonMasks.L1;
        if ((report[9] & 0x02) != 0) buttons |= PadButtonMasks.R1;
        if ((report[9] & 0x04) != 0) buttons |= PadButtonMasks.L2;
        if ((report[9] & 0x08) != 0) buttons |= PadButtonMasks.R2;
        if ((report[9] & 0x20) != 0) buttons |= PadButtonMasks.Options;
        if ((report[9] & 0x40) != 0) buttons |= PadButtonMasks.L3;
        if ((report[9] & 0x80) != 0) buttons |= PadButtonMasks.R3;
        if ((report[10] & 0x02) != 0) buttons |= PadButtonMasks.TouchPad;

        var leftX = (report[1] - 127.5f) / 127.5f;
        var leftY = (report[2] - 127.5f) / 127.5f;
        var rightX = (report[3] - 127.5f) / 127.5f;
        var rightY = (report[4] - 127.5f) / 127.5f;
        PadStateMapper.ApplyRadialDeadzone(ref leftX, ref leftY, deadzone);
        PadStateMapper.ApplyRadialDeadzone(ref rightX, ref rightY, deadzone);

        state = new HostPadState(
            buttons,
            PadStateMapper.NormalizeStick(leftX),
            PadStateMapper.NormalizeStick(leftY),
            PadStateMapper.NormalizeStick(rightX),
            PadStateMapper.NormalizeStick(rightY),
            report[5],
            report[6],
            true);

        var gyroX = BinaryPrimitives.ReadInt16LittleEndian(report[16..]);
        var gyroY = BinaryPrimitives.ReadInt16LittleEndian(report[18..]);
        var gyroZ = BinaryPrimitives.ReadInt16LittleEndian(report[20..]);
        var accelX = BinaryPrimitives.ReadInt16LittleEndian(report[22..]);
        var accelY = BinaryPrimitives.ReadInt16LittleEndian(report[24..]);
        var accelZ = BinaryPrimitives.ReadInt16LittleEndian(report[26..]);
        ReadTouch(report[33..37], out var touch0X, out var touch0Y, out var touch0Id, out var touch0Active);
        ReadTouch(report[37..41], out var touch1X, out var touch1Y, out var touch1Id, out var touch1Active);

        motion = new HostPadMotionState(
            0, 0, 0, 1,
            accelX / 8192f,
            accelY / 8192f,
            accelZ / 8192f,
            gyroX * (MathF.PI / (180f * 16f)),
            gyroY * (MathF.PI / (180f * 16f)),
            gyroZ * (MathF.PI / (180f * 16f)),
            touch0X, touch0Y, touch0Id, touch0Active,
            touch1X, touch1Y, touch1Id, touch1Active);
        return true;
    }

    internal static byte[] CreateUsbOutput(
        byte largeMotor,
        byte smallMotor,
        byte red,
        byte green,
        byte blue,
        in DualSenseTriggerEffect leftTrigger,
        in DualSenseTriggerEffect rightTrigger)
    {
        var report = new byte[UsbOutputLength];
        report[0] = 0x02;
        report[1] = 0xFF;
        report[2] = 0xF7;
        report[3] = smallMotor;
        report[4] = largeMotor;
        WriteTrigger(report.AsSpan(11, 11), rightTrigger);
        WriteTrigger(report.AsSpan(22, 11), leftTrigger);
        report[39] = 0x02;
        report[43] = 0x04;
        report[44] = red;
        report[45] = green;
        report[46] = blue;
        return report;
    }

    private static void WriteTrigger(Span<byte> destination, in DualSenseTriggerEffect effect)
    {
        destination.Clear();
        destination[0] = effect.Mode;
        if (effect.Mode != 0x01)
        {
            return;
        }

        var start = Math.Min(effect.StartPosition, (byte)9);
        var force = Math.Min(effect.Force, (byte)8);
        var activeZones = (ushort)(0x3FF & ~((1 << start) - 1));
        uint strengths = 0;
        for (var zone = start; zone < 10; zone++)
        {
            strengths |= (uint)force << (zone * 3);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(destination[1..], activeZones);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[3..], strengths);
    }

    private static void ReadTouch(
        ReadOnlySpan<byte> data,
        out ushort x,
        out ushort y,
        out byte id,
        out bool active)
    {
        id = (byte)(data[0] & 0x7F);
        active = (data[0] & 0x80) == 0;
        x = (ushort)(data[1] | ((data[2] & 0x0F) << 8));
        y = (ushort)((data[2] >> 4) | (data[3] << 4));
    }
}
