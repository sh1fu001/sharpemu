// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using SharpEmu.Logging;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SharpEmu.Libs.Keyboard;

public static class KeyboardExports
{
    private const int KeyboardHandle = 1;
    private const int KeyboardStateSize = 0x60;
    private const int KeycodeOffset = 0x20;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Keyboard");
    private static bool _initialized;
    private static ushort _lastLoggedKeycode = ushort.MaxValue;

    [SysAbiExport(
        Nid = "__hle_keyboard_init",
        ExportName = "sceKeyboardInit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceKeyboard")]
    public static int KeyboardInit(CpuContext ctx)
    {
        _initialized = true;
        return SetReturn(ctx, 0);
    }

    [SysAbiExport(
        Nid = "__hle_keyboard_open",
        ExportName = "sceKeyboardOpen",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceKeyboard")]
    public static int KeyboardOpen(CpuContext ctx) =>
        SetReturn(ctx, _initialized ? KeyboardHandle : unchecked((int)0x80960002));

    [SysAbiExport(
        Nid = "__hle_keyboard_close",
        ExportName = "sceKeyboardClose",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceKeyboard")]
    public static int KeyboardClose(CpuContext ctx) =>
        SetReturn(ctx, unchecked((int)ctx[CpuRegister.Rdi]) == KeyboardHandle ? 0 : unchecked((int)0x80960003));

    [SysAbiExport(
        Nid = "__hle_keyboard_read_state",
        ExportName = "sceKeyboardReadState",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceKeyboard")]
    public static int KeyboardReadState(CpuContext ctx)
    {
        if (unchecked((int)ctx[CpuRegister.Rdi]) != KeyboardHandle)
        {
            return SetReturn(ctx, unchecked((int)0x80960003));
        }

        var stateAddress = ctx[CpuRegister.Rsi];
        if (stateAddress == 0)
        {
            return SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> state = stackalloc byte[KeyboardStateSize];
        state.Clear();
        state[0x10] = 1;
        var keycode = ReadHostKeycode();
        if (keycode != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(state[KeycodeOffset..], keycode);
        }

        LogKeyTransition(keycode);
        return KernelMemoryCompatExports.TryWriteCompat(ctx, stateAddress, state)
            ? SetReturn(ctx, 0)
            : SetReturn(ctx, (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    private static ushort ReadHostKeycode()
    {
        var configured = ReadConfiguredKeycode();
        if (configured != 0 ||
            !OperatingSystem.IsWindows() ||
            !IsEmulatorForeground())
        {
            return configured;
        }

        if (IsKeyDown(VkReturn)) return 0x28;
        if (IsKeyDown(VkEscape)) return 0x29;
        if (IsKeyDown(VkSpace)) return 0x2C;
        if (IsKeyDown(VkRight)) return 0x4F;
        if (IsKeyDown(VkLeft)) return 0x50;
        if (IsKeyDown(VkDown)) return 0x51;
        if (IsKeyDown(VkUp)) return 0x52;
        return 0;
    }

    private static ushort ReadConfiguredKeycode()
    {
        var stateFile = Environment.GetEnvironmentVariable("SHARPEMU_KEYBOARD_STATE_FILE");
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

            return ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var keycode)
                ? keycode
                : (ushort)0;
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

    private static void LogKeyTransition(ushort keycode)
    {
        if (Environment.GetEnvironmentVariable("SHARPEMU_LOG_KEYBOARD") != "1" ||
            keycode == _lastLoggedKeycode)
        {
            return;
        }

        _lastLoggedKeycode = keycode;
        Log.Info($"Host keyboard HID keycode=0x{keycode:X4}");
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
