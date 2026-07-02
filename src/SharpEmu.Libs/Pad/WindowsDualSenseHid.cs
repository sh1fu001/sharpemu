// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using Microsoft.Win32.SafeHandles;
using SharpEmu.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SharpEmu.Libs.Pad;

internal sealed class WindowsDualSenseHid : IDisposable
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("DualSenseHid");

    private readonly FileStream _stream;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _reader;
    private readonly float _deadzone;
    private readonly object _stateLock = new();
    private HostPadState _state = HostPadState.Neutral;
    private int _lastOutput = -1;
    private bool _inputConfirmed;
    private bool _disposed;

    private WindowsDualSenseHid(FileStream stream, string path, float deadzone)
    {
        _stream = stream;
        _deadzone = deadzone;
        _reader = Task.Run(ReadLoopAsync);
        Log.Info($"Native DualSense HID connected: transport=USB, path={path}");
    }

    internal static WindowsDualSenseHid? TryCreate(float deadzone)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var path in EnumerateHidPaths())
        {
            SafeFileHandle? handle = null;
            try
            {
                handle = CreateFile(
                    path,
                    GenericRead | GenericWrite,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    FileFlagOverlapped,
                    IntPtr.Zero);
                if (handle.IsInvalid || !TryGetAttributes(handle, out var attributes) ||
                    !DualSenseHidReport.IsSupported(attributes.VendorId, attributes.ProductId))
                {
                    handle?.Dispose();
                    continue;
                }

                var stream = new FileStream(handle, FileAccess.ReadWrite, DualSenseHidReport.UsbInputLength, true);
                handle = null;
                return new WindowsDualSenseHid(stream, path, deadzone);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or Win32Exception)
            {
                handle?.Dispose();
            }
        }

        return null;
    }

    internal bool TryReadState(out HostPadState state)
    {
        lock (_stateLock)
        {
            state = _state;
            return state.GamepadConnected;
        }
    }

    internal void ApplyOutput(
        byte largeMotor,
        byte smallMotor,
        byte red,
        byte green,
        byte blue,
        in DualSenseTriggerEffect leftTrigger,
        in DualSenseTriggerEffect rightTrigger)
    {
        var packed = largeMotor | (smallMotor << 8) | (red << 16) | (green << 24);
        packed = HashCode.Combine(packed, blue, leftTrigger, rightTrigger);
        if (_disposed || packed == _lastOutput)
        {
            return;
        }

        _lastOutput = packed;
        try
        {
            var report = DualSenseHidReport.CreateUsbOutput(
                largeMotor,
                smallMotor,
                red,
                green,
                blue,
                leftTrigger,
                rightTrigger);
            _stream.Write(report);
        }
        catch (IOException exception)
        {
            Log.Warn($"DualSense HID output failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        try
        {
            _reader.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _stream.Dispose();
        _cancellation.Dispose();
        HostPadMotionStateCache.Reset();
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[DualSenseHidReport.UsbInputLength];
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var offset = 0;
                while (offset < buffer.Length)
                {
                    var read = await _stream.ReadAsync(
                        buffer.AsMemory(offset),
                        _cancellation.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    offset += read;
                }

                if (DualSenseHidReport.TryParse(buffer, _deadzone, out var state, out var motion))
                {
                    lock (_stateLock)
                    {
                        _state = state;
                    }

                    HostPadMotionStateCache.Publish(motion);
                    if (!_inputConfirmed)
                    {
                        _inputConfirmed = true;
                        Log.Info(
                            $"Native DualSense input active: report_bytes={buffer.Length}, " +
                            $"touch={motion.Touch0Active || motion.Touch1Active}");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or EndOfStreamException)
        {
            lock (_stateLock)
            {
                _state = HostPadState.Neutral;
            }

            HostPadMotionStateCache.Reset();
            Log.Warn($"DualSense HID disconnected: {exception.Message}");
        }
    }

    private static IEnumerable<string> EnumerateHidPaths()
    {
        HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid,
            null,
            IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == InvalidHandleValue)
        {
            yield break;
        }

        try
        {
            for (var index = 0u; ; index++)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    Size = (uint)Marshal.SizeOf<SpDeviceInterfaceData>(),
                };
                if (!SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref hidGuid,
                        index,
                        ref interfaceData))
                {
                    if (Marshal.GetLastWin32Error() == ErrorNoMoreItems)
                    {
                        yield break;
                    }

                    continue;
                }

                _ = SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);
                if (requiredSize == 0)
                {
                    continue;
                }

                var detail = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref interfaceData,
                            detail,
                            requiredSize,
                            out _,
                            IntPtr.Zero))
                    {
                        var path = Marshal.PtrToStringUni(detail + 4);
                        if (!string.IsNullOrEmpty(path))
                        {
                            yield return path;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static bool TryGetAttributes(SafeFileHandle handle, out HiddAttributes attributes)
    {
        attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        return HidD_GetAttributes(handle, ref attributes);
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const int ErrorNoMoreItems = 259;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        internal uint Size;
        internal Guid InterfaceClassGuid;
        internal uint Flags;
        internal UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        internal int Size;
        internal ushort VendorId;
        internal ushort ProductId;
        internal ushort VersionNumber;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetAttributes(
        SafeFileHandle hidDeviceObject,
        ref HiddAttributes attributes);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
