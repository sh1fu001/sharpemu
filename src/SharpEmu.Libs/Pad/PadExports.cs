// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using SharpEmu.Logging;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

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
    private const uint PadButtonOptions = 0x00000008;
    private const uint PadButtonUp = 0x00000010;
    private const uint PadButtonRight = 0x00000020;
    private const uint PadButtonDown = 0x00000040;
    private const uint PadButtonLeft = 0x00000080;
    private const uint PadButtonTriangle = 0x00001000;
    private const uint PadButtonCircle = 0x00002000;
    private const uint PadButtonCross = 0x00004000;
    private const uint PadButtonSquare = 0x00008000;

    private const int VkBack = 0x08;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkA = 0x41;
    private const int VkD = 0x44;
    private const int VkQ = 0x51;
    private const int VkS = 0x53;
    private const int VkW = 0x57;
    private const int VkX = 0x58;
    private const int VkZ = 0x5A;

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
        Nid = "__hle_pad_vibration",
        ExportName = "scePadSetVibration",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetVibration(CpuContext ctx) =>
        ValidateOpenHandle(ctx);

    [SysAbiExport(
        Nid = "__hle_pad_light_bar",
        ExportName = "scePadSetLightBar",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadSetLightBar(CpuContext ctx) =>
        ValidateOpenHandle(ctx);

    [SysAbiExport(
        Nid = "__hle_pad_close",
        ExportName = "scePadClose",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libScePad")]
    public static int PadClose(CpuContext ctx) =>
        ValidateOpenHandle(ctx);

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

        return WriteNeutralPadData(ctx, dataAddress)
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

        return WriteNeutralPadData(ctx, dataAddress)
            ? SetReturn(ctx, 1)
            : SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    private static bool WriteNeutralPadData(CpuContext ctx, ulong dataAddress)
    {
        Span<byte> data = stackalloc byte[PadDataSize];
        data.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(data, ReadHostButtons());
        data[0x04] = 128;
        data[0x05] = 128;
        data[0x06] = 128;
        data[0x07] = 128;
        BinaryPrimitives.WriteSingleLittleEndian(data[0x18..], 1.0f);
        data[0x4C] = 1;
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

    private static int ValidateOpenHandle(CpuContext ctx) =>
        unchecked((int)ctx[CpuRegister.Rdi]) == PrimaryPadHandle
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, OrbisPadErrorInvalidHandle);

    private static uint ReadHostButtons()
    {
        var buttons = ReadConfiguredButtons();
        if (!OperatingSystem.IsWindows() || !IsEmulatorForeground())
        {
            LogButtonTransition(buttons);
            return buttons;
        }

        if (IsKeyDown(VkUp) || IsKeyDown(VkW)) buttons |= PadButtonUp;
        if (IsKeyDown(VkRight) || IsKeyDown(VkD)) buttons |= PadButtonRight;
        if (IsKeyDown(VkDown) || IsKeyDown(VkS)) buttons |= PadButtonDown;
        if (IsKeyDown(VkLeft) || IsKeyDown(VkA) || IsKeyDown(VkQ)) buttons |= PadButtonLeft;
        if (IsKeyDown(VkReturn) || IsKeyDown(VkSpace) || IsKeyDown(VkZ)) buttons |= PadButtonCross;
        if (IsKeyDown(VkEscape) || IsKeyDown(VkBack) || IsKeyDown(VkX)) buttons |= PadButtonCircle;
        if (IsKeyDown(0x52)) buttons |= PadButtonTriangle;
        if (IsKeyDown(0x46)) buttons |= PadButtonSquare;
        if (IsKeyDown(0x50)) buttons |= PadButtonOptions;
        LogButtonTransition(buttons);
        return buttons;
    }

    private static void LogButtonTransition(uint buttons)
    {
        if (Environment.GetEnvironmentVariable("SHARPEMU_LOG_PAD") != "1" ||
            buttons == _lastLoggedButtons)
        {
            return;
        }

        _lastLoggedButtons = buttons;
        Log.Info(
            $"Host pad buttons=0x{buttons:X8}, state_file=" +
            $"{Environment.GetEnvironmentVariable("SHARPEMU_PAD_STATE_FILE") ?? "<unset>"}");
    }

    private static uint ReadConfiguredButtons()
    {
        var stateFile = Environment.GetEnvironmentVariable("SHARPEMU_PAD_STATE_FILE");
        if (string.IsNullOrWhiteSpace(stateFile))
        {
            return 0;
        }

        try
        {
            var value = File.ReadAllText(stateFile).Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value[2..];
            }

            return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var buttons)
                ? buttons
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static bool IsKeyDown(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsEmulatorForeground()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == 0)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        return processId == Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out int processId);

    private static int SetReturn(CpuContext ctx, int result)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)result);
        return result;
    }
}
