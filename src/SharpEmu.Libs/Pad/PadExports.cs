// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using SharpEmu.Logging;
using System.Buffers.Binary;
using System.Diagnostics;

namespace SharpEmu.Libs.Pad;

public static class PadExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Pad");

    private const int OrbisPadErrorInvalidHandle = unchecked((int)0x80920003);
    private const int OrbisPadErrorNotInitialized = unchecked((int)0x80920005);
    private const int OrbisPadErrorDeviceNotConnected = unchecked((int)0x80920007);
    private const int OrbisPadErrorDeviceNoHandle = unchecked((int)0x80920008);
    private const int PrimaryUserId = 1;
    private const int StandardPortType = 0;
    private const int PrimaryPadHandle = 1;
    private const int ControllerInformationSize = 0x1C;
    private const int PadDataSize = 0x78;

    private static readonly bool LogPadTransitions =
        string.Equals(
            Environment.GetEnvironmentVariable("SHARPEMU_LOG_PAD"),
            "1",
            StringComparison.Ordinal);
    private static readonly string? PadStateFile =
        Environment.GetEnvironmentVariable("SHARPEMU_PAD_STATE_FILE");

    private static bool _initialized;
    private static uint _lastLoggedButtons = uint.MaxValue;

    [SysAbiExport(
        Nid = "hv1luiJrqQM",
        ExportName = "scePadInit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadInit(CpuContext ctx)
    {
        _initialized = true;
        return SetReturn(ctx, 0);
    }

    [SysAbiExport(
        Nid = "xk0AcarP3V4",
        ExportName = "scePadOpen",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadOpen(CpuContext ctx)
    {
        var userId = unchecked((int)ctx[CpuRegister.Rdi]);
        var type = unchecked((int)ctx[CpuRegister.Rsi]);
        var index = unchecked((int)ctx[CpuRegister.Rdx]);
        var parameterAddress = ctx[CpuRegister.Rcx];
        if (!_initialized)
        {
            return SetReturn(ctx, OrbisPadErrorNotInitialized);
        }

        if (userId == -1)
        {
            return SetReturn(ctx, OrbisPadErrorDeviceNoHandle);
        }

        if (userId != PrimaryUserId || type != StandardPortType || index != 0 || parameterAddress != 0)
        {
            return SetReturn(ctx, OrbisPadErrorDeviceNotConnected);
        }

        return SetReturn(ctx, PrimaryPadHandle);
    }

    [SysAbiExport(
        Nid = "clVvL4ZDntw",
        ExportName = "scePadSetMotionSensorState",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetMotionSensorState(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        return handle == PrimaryPadHandle
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, OrbisPadErrorInvalidHandle);
    }

    [SysAbiExport(
        Nid = "gjP9-KQzoUk",
        ExportName = "scePadGetControllerInformation",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadGetControllerInformation(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var informationAddress = ctx[CpuRegister.Rsi];
        if (handle != PrimaryPadHandle)
        {
            return SetReturn(ctx, OrbisPadErrorInvalidHandle);
        }

        if (informationAddress == 0)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> information = stackalloc byte[ControllerInformationSize];
        BinaryPrimitives.WriteSingleLittleEndian(information[0x00..], 44.86f);
        BinaryPrimitives.WriteUInt16LittleEndian(information[0x04..], 1920);
        BinaryPrimitives.WriteUInt16LittleEndian(information[0x06..], 943);
        information[0x08] = 30;
        information[0x09] = 30;
        information[0x0A] = StandardPortType;
        information[0x0B] = 1;
        information[0x0C] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(information[0x10..], 0);

        return KernelMemoryCompatExports.TryWriteCompat(ctx, informationAddress, information)
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    [SysAbiExport(
        Nid = "__hle_pad_vibration_mode",
        ExportName = "scePadSetVibrationMode",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetVibrationMode(CpuContext ctx) =>
        ValidateOpenHandle(ctx);

    [SysAbiExport(
        Nid = "yFVnOdGxvZY",
        ExportName = "scePadSetVibration",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetVibration(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var parameterAddress = ctx[CpuRegister.Rsi];
        if (handle != PrimaryPadHandle)
        {
            return SetReturn(ctx, OrbisPadErrorInvalidHandle);
        }

        if (parameterAddress == 0)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> vibration = stackalloc byte[2];
        if (!KernelMemoryCompatExports.TryReadCompat(ctx, parameterAddress, vibration))
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        HostPadOutputCache.SetVibration(vibration[0], vibration[1]);
        return SetReturn(ctx, 0);
    }

    [SysAbiExport(
        Nid = "RR4novUEENY",
        ExportName = "scePadSetLightBar",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetLightBar(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var parameterAddress = ctx[CpuRegister.Rsi];
        if (handle != PrimaryPadHandle)
        {
            return SetReturn(ctx, OrbisPadErrorInvalidHandle);
        }

        if (parameterAddress == 0)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> color = stackalloc byte[3];
        if (!KernelMemoryCompatExports.TryReadCompat(ctx, parameterAddress, color))
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        HostPadOutputCache.SetLightBar(color[0], color[1], color[2]);
        return SetReturn(ctx, 0);
    }

    [SysAbiExport(
        Nid = "6ncge5+l5Qs",
        ExportName = "scePadClose",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadClose(CpuContext ctx)
    {
        var result = ValidateOpenHandle(ctx);
        if (result == 0)
        {
            HostPadOutputCache.SetVibration(0, 0);
        }

        return result;
    }

    [SysAbiExport(
        Nid = "YndgXqQVV7c",
        ExportName = "scePadReadState",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadReadState(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var dataAddress = ctx[CpuRegister.Rsi];
        if (handle != PrimaryPadHandle)
        {
            return SetReturn(ctx, OrbisPadErrorInvalidHandle);
        }

        if (dataAddress == 0)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        return WritePadData(ctx, dataAddress)
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    [SysAbiExport(
        Nid = "q1cHNfGycLI",
        ExportName = "scePadRead",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadRead(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var dataAddress = ctx[CpuRegister.Rsi];
        var count = unchecked((int)ctx[CpuRegister.Rdx]);
        if (handle != PrimaryPadHandle)
        {
            return SetReturn(ctx, OrbisPadErrorInvalidHandle);
        }

        if (dataAddress == 0 || count < 1 || count > 64)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        return WritePadData(ctx, dataAddress)
            ? SetReturn(ctx, 1)
            : SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    private static bool WritePadData(CpuContext ctx, ulong dataAddress)
    {
        var hostState = HostPadStateCache.Read();
        var buttons = hostState.Buttons | ConfiguredPadButtons.Read();
        LogButtonTransition(buttons, hostState.GamepadConnected);

        Span<byte> data = stackalloc byte[PadDataSize];
        data.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(data, buttons);
        data[0x04] = hostState.LeftStickX;
        data[0x05] = hostState.LeftStickY;
        data[0x06] = hostState.RightStickX;
        data[0x07] = hostState.RightStickY;
        data[0x08] = hostState.LeftTrigger;
        data[0x09] = hostState.RightTrigger;
        BinaryPrimitives.WriteSingleLittleEndian(data[0x18..], 1.0f);
        var motion = HostPadMotionStateCache.Read();
        BinaryPrimitives.WriteSingleLittleEndian(data[0x0C..], motion.OrientationX);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x10..], motion.OrientationY);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x14..], motion.OrientationZ);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x18..], motion.OrientationW);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x1C..], motion.AccelerationX);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x20..], motion.AccelerationY);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x24..], motion.AccelerationZ);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x28..], motion.AngularVelocityX);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x2C..], motion.AngularVelocityY);
        BinaryPrimitives.WriteSingleLittleEndian(data[0x30..], motion.AngularVelocityZ);
        var touchCount = 0;
        if (motion.Touch0Active)
        {
            WriteTouch(data[(0x38 + (touchCount * 8))..], motion.Touch0X, motion.Touch0Y, motion.Touch0Id);
            touchCount++;
        }

        if (motion.Touch1Active)
        {
            WriteTouch(data[(0x38 + (touchCount * 8))..], motion.Touch1X, motion.Touch1Y, motion.Touch1Id);
            touchCount++;
        }

        data[0x34] = (byte)touchCount;
        data[0x48] = hostState.GamepadConnected ? (byte)1 : (byte)0;
        var timestampTicks = Stopwatch.GetTimestamp();
        var timestampMicroseconds =
            ((ulong)(timestampTicks / Stopwatch.Frequency) * 1_000_000UL) +
            ((ulong)(timestampTicks % Stopwatch.Frequency) * 1_000_000UL / (ulong)Stopwatch.Frequency);
        BinaryPrimitives.WriteUInt64LittleEndian(
            data[0x50..],
            timestampMicroseconds);
        data[0x68] = 1;

        return KernelMemoryCompatExports.TryWriteCompat(ctx, dataAddress, data);
    }

    private static void WriteTouch(
        Span<byte> destination,
        ushort x,
        ushort y,
        byte id)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, x);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], y);
        destination[4] = id;
    }

    private static int ValidateOpenHandle(CpuContext ctx) =>
        unchecked((int)ctx[CpuRegister.Rdi]) == PrimaryPadHandle
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, OrbisPadErrorInvalidHandle);

    private static void LogButtonTransition(uint buttons, bool gamepadConnected)
    {
        if (!LogPadTransitions || buttons == _lastLoggedButtons)
        {
            return;
        }

        _lastLoggedButtons = buttons;
        Log.Info(
            $"Host pad buttons=0x{buttons:X8}, gamepad_connected={gamepadConnected}, state_file=" +
            $"{PadStateFile ?? "<unset>"}");
    }

    private static int SetReturn(CpuContext ctx, int result)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)result);
        return result;
    }
}
