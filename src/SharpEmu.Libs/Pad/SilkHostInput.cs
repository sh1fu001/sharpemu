// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Logging;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Globalization;

namespace SharpEmu.Libs.Pad;

internal sealed class SilkHostInput : IDisposable
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("HostInput");

    private readonly IInputContext _context;
    private readonly int? _preferredGamepadIndex;
    private readonly float _deadzone;
    private readonly bool _gamepadEnabled;

    private IGamepad? _activeGamepad;
    private string? _activeGamepadName;
    private int _lastVibration = -1;
    private bool _selectionInitialized;
    private bool _selectionDirty = true;
    private bool _disposed;

    private SilkHostInput(IInputContext context)
    {
        _context = context;
        _preferredGamepadIndex = ParseGamepadIndex();
        _deadzone = ParseDeadzone();
        _gamepadEnabled = !string.Equals(
            Environment.GetEnvironmentVariable("SHARPEMU_DISABLE_GAMEPAD"),
            "1",
            StringComparison.Ordinal);
        _context.ConnectionChanged += OnConnectionChanged;
    }

    internal static SilkHostInput? TryCreate(IWindow window)
    {
        try
        {
            return new SilkHostInput(window.CreateInput());
        }
        catch (Exception exception)
        {
            Log.Warn($"Host input initialization failed: {exception.Message}");
            HostPadStateCache.Reset();
            return null;
        }
    }

    internal void Poll(bool windowFocused)
    {
        if (_disposed)
        {
            return;
        }

        var gamepad = _gamepadEnabled ? SelectGamepad() : null;
        if (!windowFocused)
        {
            HostPadStateCache.Reset();
            ApplyVibration(gamepad, 0, 0);
            return;
        }

        var state = PadStateMapper.Create(gamepad, _context.Keyboards, _deadzone);
        HostPadStateCache.Publish(state);

        var vibration = HostPadOutputCache.ReadVibration();
        ApplyVibration(gamepad, vibration.LargeMotor, vibration.SmallMotor);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ApplyVibration(_activeGamepad, 0, 0);
        HostPadStateCache.Reset();
        _context.ConnectionChanged -= OnConnectionChanged;
        _context.Dispose();
        _activeGamepad = null;
        _activeGamepadName = null;
    }

    private IGamepad? SelectGamepad()
    {
        if (!_selectionDirty &&
            _activeGamepad is { IsConnected: true } active &&
            (_preferredGamepadIndex is null || active.Index == _preferredGamepadIndex))
        {
            return active;
        }

        IGamepad? selected = null;
        for (var index = 0; index < _context.Gamepads.Count; index++)
        {
            var candidate = _context.Gamepads[index];
            if (!candidate.IsConnected)
            {
                continue;
            }

            if (_preferredGamepadIndex is { } preferredIndex)
            {
                if (candidate.Index == preferredIndex)
                {
                    selected = candidate;
                    break;
                }

                continue;
            }

            selected ??= candidate;
            if (IsDualSense(candidate.Name))
            {
                selected = candidate;
                break;
            }
        }

        if (_selectionInitialized && ReferenceEquals(selected, _activeGamepad))
        {
            return selected;
        }

        var wasInitialized = _selectionInitialized;
        _selectionInitialized = true;
        _selectionDirty = false;
        ApplyVibration(_activeGamepad, 0, 0);
        _activeGamepad = selected;
        _lastVibration = -1;

        var selectedName = selected?.Name;
        if (!wasInitialized ||
            !string.Equals(selectedName, _activeGamepadName, StringComparison.Ordinal))
        {
            _activeGamepadName = selectedName;
            if (selected is null)
            {
                Log.Info("No host gamepad detected; keyboard input remains available.");
            }
            else
            {
                Log.Info(
                    $"Host gamepad connected: index={selected.Index}, name={selectedName}, " +
                    $"dualsense={IsDualSense(selectedName)}, deadzone={_deadzone:0.00}");
            }
        }

        return selected;
    }

    private void OnConnectionChanged(IInputDevice device, bool _)
    {
        if (device is IGamepad)
        {
            _selectionDirty = true;
        }
    }

    private void ApplyVibration(IGamepad? gamepad, byte largeMotor, byte smallMotor)
    {
        var packed = largeMotor | (smallMotor << 8);
        if (packed == _lastVibration)
        {
            return;
        }

        _lastVibration = packed;
        if (gamepad is null || gamepad.VibrationMotors.Count == 0)
        {
            return;
        }

        gamepad.VibrationMotors[0].Speed = largeMotor / 255f;
        if (gamepad.VibrationMotors.Count > 1)
        {
            gamepad.VibrationMotors[1].Speed = smallMotor / 255f;
        }
    }

    private static bool IsDualSense(string? name) =>
        name is not null &&
        (name.Contains("DualSense", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("PS5", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase));

    private static int? ParseGamepadIndex()
    {
        var value = Environment.GetEnvironmentVariable("SHARPEMU_GAMEPAD_INDEX");
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
               index >= 0
            ? index
            : null;
    }

    private static float ParseDeadzone()
    {
        var value = Environment.GetEnvironmentVariable("SHARPEMU_GAMEPAD_DEADZONE");
        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var deadzone)
            ? Math.Clamp(deadzone, 0f, 0.95f)
            : PadStateMapper.DefaultDeadzone;
    }
}
