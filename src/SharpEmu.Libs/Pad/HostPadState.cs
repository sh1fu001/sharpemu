// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using Silk.NET.Input;
using System.Diagnostics;
using System.Globalization;

namespace SharpEmu.Libs.Pad;

internal static class PadButtonMasks
{
    internal const uint L3 = 0x00000002;
    internal const uint R3 = 0x00000004;
    internal const uint Options = 0x00000008;
    internal const uint Up = 0x00000010;
    internal const uint Right = 0x00000020;
    internal const uint Down = 0x00000040;
    internal const uint Left = 0x00000080;
    internal const uint L2 = 0x00000100;
    internal const uint R2 = 0x00000200;
    internal const uint L1 = 0x00000400;
    internal const uint R1 = 0x00000800;
    internal const uint Triangle = 0x00001000;
    internal const uint Circle = 0x00002000;
    internal const uint Cross = 0x00004000;
    internal const uint Square = 0x00008000;
    internal const uint TouchPad = 0x00100000;
}

internal readonly record struct HostPadState(
    uint Buttons,
    byte LeftStickX,
    byte LeftStickY,
    byte RightStickX,
    byte RightStickY,
    byte LeftTrigger,
    byte RightTrigger,
    bool GamepadConnected)
{
    internal static HostPadState Neutral { get; } =
        new(0, 128, 128, 128, 128, 0, 0, false);
}

/// <summary>
/// A single-writer seqlock used by the window input thread and the guest CPU threads.
/// A pad read is two volatile payload reads in the uncontended path and never allocates.
/// </summary>
internal static class HostPadStateCache
{
    private static long _version;
    private static long _buttonsAndSticks = unchecked((long)(
        (128UL << 32) |
        (128UL << 40) |
        (128UL << 48) |
        (128UL << 56)));
    private static long _triggersAndConnection;

    internal static void Publish(in HostPadState state)
    {
        Interlocked.Increment(ref _version);
        Volatile.Write(ref _buttonsAndSticks, unchecked((long)PackButtonsAndSticks(state)));
        Volatile.Write(ref _triggersAndConnection, unchecked((long)PackTriggersAndConnection(state)));
        Interlocked.Increment(ref _version);
    }

    internal static HostPadState Read()
    {
        while (true)
        {
            var before = Volatile.Read(ref _version);
            if ((before & 1) != 0)
            {
                Thread.SpinWait(1);
                continue;
            }

            var buttonsAndSticks = unchecked((ulong)Volatile.Read(ref _buttonsAndSticks));
            var triggersAndConnection = unchecked((ulong)Volatile.Read(ref _triggersAndConnection));
            var after = Volatile.Read(ref _version);
            if (before == after)
            {
                return Unpack(buttonsAndSticks, triggersAndConnection);
            }
        }
    }

    internal static void Reset() => Publish(HostPadState.Neutral);

    private static ulong PackButtonsAndSticks(in HostPadState state) =>
        state.Buttons |
        ((ulong)state.LeftStickX << 32) |
        ((ulong)state.LeftStickY << 40) |
        ((ulong)state.RightStickX << 48) |
        ((ulong)state.RightStickY << 56);

    private static ulong PackTriggersAndConnection(in HostPadState state) =>
        state.LeftTrigger |
        ((ulong)state.RightTrigger << 8) |
        (state.GamepadConnected ? 1UL << 16 : 0);

    private static HostPadState Unpack(ulong buttonsAndSticks, ulong triggersAndConnection) =>
        new(
            (uint)buttonsAndSticks,
            (byte)(buttonsAndSticks >> 32),
            (byte)(buttonsAndSticks >> 40),
            (byte)(buttonsAndSticks >> 48),
            (byte)(buttonsAndSticks >> 56),
            (byte)triggersAndConnection,
            (byte)(triggersAndConnection >> 8),
            (triggersAndConnection & (1UL << 16)) != 0);
}

internal static class HostPadOutputCache
{
    private static int _vibration;

    internal static void SetVibration(byte largeMotor, byte smallMotor) =>
        Volatile.Write(ref _vibration, largeMotor | (smallMotor << 8));

    internal static (byte LargeMotor, byte SmallMotor) ReadVibration()
    {
        var packed = Volatile.Read(ref _vibration);
        return ((byte)packed, (byte)(packed >> 8));
    }
}

internal static class PadStateMapper
{
    internal const float DefaultDeadzone = 0.12f;
    internal const byte DigitalTriggerThreshold = 30;

    internal static HostPadState Create(
        IGamepad? gamepad,
        IReadOnlyList<IKeyboard> keyboards,
        float deadzone)
    {
        var buttons = ReadKeyboardButtons(keyboards);
        if (gamepad is null || !gamepad.IsConnected)
        {
            return HostPadState.Neutral with { Buttons = buttons };
        }

        buttons |= ReadGamepadButtons(gamepad.Buttons);

        var leftX = 0f;
        var leftY = 0f;
        var rightX = 0f;
        var rightY = 0f;
        if (gamepad.Thumbsticks.Count > 0)
        {
            leftX = gamepad.Thumbsticks[0].X;
            leftY = gamepad.Thumbsticks[0].Y;
            ApplyRadialDeadzone(ref leftX, ref leftY, deadzone);
        }

        if (gamepad.Thumbsticks.Count > 1)
        {
            rightX = gamepad.Thumbsticks[1].X;
            rightY = gamepad.Thumbsticks[1].Y;
            ApplyRadialDeadzone(ref rightX, ref rightY, deadzone);
        }

        var leftTrigger = gamepad.Triggers.Count > 0
            ? NormalizeGlfwTrigger(gamepad.Triggers[0].Position)
            : (byte)0;
        var rightTrigger = gamepad.Triggers.Count > 1
            ? NormalizeGlfwTrigger(gamepad.Triggers[1].Position)
            : (byte)0;

        if (leftTrigger >= DigitalTriggerThreshold)
        {
            buttons |= PadButtonMasks.L2;
        }

        if (rightTrigger >= DigitalTriggerThreshold)
        {
            buttons |= PadButtonMasks.R2;
        }

        return new HostPadState(
            buttons,
            NormalizeStick(leftX),
            NormalizeStick(leftY),
            NormalizeStick(rightX),
            NormalizeStick(rightY),
            leftTrigger,
            rightTrigger,
            true);
    }

    internal static uint ReadGamepadButtons(IReadOnlyList<Button> buttons)
    {
        var result = 0u;
        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            if (!button.Pressed)
            {
                continue;
            }

            result |= button.Name switch
            {
                ButtonName.A => PadButtonMasks.Cross,
                ButtonName.B => PadButtonMasks.Circle,
                ButtonName.X => PadButtonMasks.Square,
                ButtonName.Y => PadButtonMasks.Triangle,
                ButtonName.LeftBumper => PadButtonMasks.L1,
                ButtonName.RightBumper => PadButtonMasks.R1,
                ButtonName.Back => PadButtonMasks.TouchPad,
                ButtonName.Start => PadButtonMasks.Options,
                ButtonName.LeftStick => PadButtonMasks.L3,
                ButtonName.RightStick => PadButtonMasks.R3,
                ButtonName.DPadUp => PadButtonMasks.Up,
                ButtonName.DPadRight => PadButtonMasks.Right,
                ButtonName.DPadDown => PadButtonMasks.Down,
                ButtonName.DPadLeft => PadButtonMasks.Left,
                _ => 0,
            };
        }

        return result;
    }

    internal static byte NormalizeStick(float value)
    {
        value = Math.Clamp(value, -1f, 1f);
        return (byte)Math.Clamp((int)MathF.Round((value + 1f) * 127.5f), 0, byte.MaxValue);
    }

    internal static byte NormalizeGlfwTrigger(float value)
    {
        // GLFW exposes standard gamepad triggers in [-1, 1], with -1 released.
        var normalized = Math.Clamp((value + 1f) * 0.5f, 0f, 1f);
        return (byte)Math.Clamp((int)MathF.Round(normalized * byte.MaxValue), 0, byte.MaxValue);
    }

    internal static void ApplyRadialDeadzone(ref float x, ref float y, float deadzone)
    {
        deadzone = Math.Clamp(deadzone, 0f, 0.95f);
        var magnitudeSquared = (x * x) + (y * y);
        if (magnitudeSquared <= deadzone * deadzone)
        {
            x = 0;
            y = 0;
            return;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        var clampedMagnitude = Math.Min(magnitude, 1f);
        var scaledMagnitude = (clampedMagnitude - deadzone) / (1f - deadzone);
        var scale = scaledMagnitude / magnitude;
        x *= scale;
        y *= scale;
    }

    private static uint ReadKeyboardButtons(IReadOnlyList<IKeyboard> keyboards)
    {
        IKeyboard? keyboard = null;
        for (var index = 0; index < keyboards.Count; index++)
        {
            if (keyboards[index].IsConnected)
            {
                keyboard = keyboards[index];
                break;
            }
        }

        if (keyboard is null)
        {
            return 0;
        }

        var buttons = 0u;
        if (keyboard.IsKeyPressed(Key.Up) || keyboard.IsKeyPressed(Key.W)) buttons |= PadButtonMasks.Up;
        if (keyboard.IsKeyPressed(Key.Right) || keyboard.IsKeyPressed(Key.D)) buttons |= PadButtonMasks.Right;
        if (keyboard.IsKeyPressed(Key.Down) || keyboard.IsKeyPressed(Key.S)) buttons |= PadButtonMasks.Down;
        if (keyboard.IsKeyPressed(Key.Left) || keyboard.IsKeyPressed(Key.A) || keyboard.IsKeyPressed(Key.Q)) buttons |= PadButtonMasks.Left;
        if (keyboard.IsKeyPressed(Key.Enter) || keyboard.IsKeyPressed(Key.Space) || keyboard.IsKeyPressed(Key.Z)) buttons |= PadButtonMasks.Cross;
        if (keyboard.IsKeyPressed(Key.Escape) || keyboard.IsKeyPressed(Key.Backspace) || keyboard.IsKeyPressed(Key.X)) buttons |= PadButtonMasks.Circle;
        if (keyboard.IsKeyPressed(Key.R)) buttons |= PadButtonMasks.Triangle;
        if (keyboard.IsKeyPressed(Key.F)) buttons |= PadButtonMasks.Square;
        if (keyboard.IsKeyPressed(Key.P)) buttons |= PadButtonMasks.Options;
        return buttons;
    }
}

internal static class ConfiguredPadButtons
{
    private static readonly string? StateFile =
        Environment.GetEnvironmentVariable("SHARPEMU_PAD_STATE_FILE");
    private static readonly long RefreshIntervalTicks =
        Math.Max(1, Stopwatch.Frequency / 60);

    private static long _nextRefresh;
    private static uint _buttons;

    internal static uint Read()
    {
        if (string.IsNullOrWhiteSpace(StateFile))
        {
            return 0;
        }

        var now = Stopwatch.GetTimestamp();
        var nextRefresh = Volatile.Read(ref _nextRefresh);
        if (now < nextRefresh ||
            Interlocked.CompareExchange(
                ref _nextRefresh,
                now + RefreshIntervalTicks,
                nextRefresh) != nextRefresh)
        {
            return Volatile.Read(ref _buttons);
        }

        Volatile.Write(ref _buttons, ReadFile(StateFile));
        return Volatile.Read(ref _buttons);
    }

    private static uint ReadFile(string path)
    {
        try
        {
            var value = File.ReadAllText(path).Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value[2..];
            }

            return uint.TryParse(
                value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var buttons)
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
}
