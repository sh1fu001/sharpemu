// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Runtime.InteropServices;
using System.Text;

namespace SharpEmu.HLE;

public static class HleDataSymbols
{
    private const string StackChkGuardNid = "f7uOxY9mM1U";
    private const string ProgNameNid = "djxxOmW6-aw";
    private const string LibcNeedFlagNid = "P330P3dFF68";
    private const string LibcInternalNeedFlagNid = "ZT4ODD2Ts9o";
    private const string IsThreadedName = "__isthreaded";
    private const string StdoutName = "__stdoutp";
    private const string StderrName = "__stderrp";
    private const string StdinName = "__stdinp";
    private const string EnvironName = "environ";
    private const int ProgNameMaxBytes = 511;
    private const ulong StackChkGuardValue = 0xC0DEC0DECAFEBABEUL;

    private static readonly object _gate = new();
    private static readonly nint _stackChkGuardAddress = Allocate(sizeof(ulong) * 2);
    private static readonly nint _progNameBufferAddress = Allocate(ProgNameMaxBytes + 1);
    private static readonly nint _progNamePointerAddress = Allocate(nint.Size);
    private static readonly nint _libcNeedFlagAddress = Allocate(sizeof(uint));
    private static readonly nint _libcInternalNeedFlagAddress = Allocate(sizeof(uint));
    private static readonly nint _isThreadedAddress = Allocate(sizeof(uint));
    private static readonly nint _stdoutPointerAddress = Allocate(nint.Size);
    private static readonly nint _stderrPointerAddress = Allocate(nint.Size);
    private static readonly nint _stdinPointerAddress = Allocate(nint.Size);
    private static readonly nint _argvAddress = Allocate(nint.Size * 2);
    private static readonly nint _environAddress = Allocate(nint.Size);

    static HleDataSymbols()
    {
        if (_stackChkGuardAddress != 0)
        {
            Marshal.WriteInt64(_stackChkGuardAddress, unchecked((long)StackChkGuardValue));
            Marshal.WriteInt64(
                IntPtr.Add(_stackChkGuardAddress, sizeof(ulong)),
                unchecked((long)StackChkGuardValue));
        }

        if (_libcNeedFlagAddress != 0)
        {
            Marshal.WriteInt32(_libcNeedFlagAddress, 1);
        }

        if (_libcInternalNeedFlagAddress != 0)
        {
            Marshal.WriteInt32(_libcInternalNeedFlagAddress, 1);
        }

        if (_isThreadedAddress != 0)
        {
            Marshal.WriteInt32(_isThreadedAddress, 0);
        }

        InitializeStreamPointer(_stdoutPointerAddress);
        InitializeStreamPointer(_stderrPointerAddress);
        InitializeStreamPointer(_stdinPointerAddress);
        if (_argvAddress != 0)
        {
            WritePointer(_argvAddress, _progNameBufferAddress);
        }

        ConfigureProcessImageName("eboot.bin");
    }

    public static IEnumerable<string> EnumerateKnownNids()
    {
        yield return StackChkGuardNid;
        yield return ProgNameNid;
        yield return LibcNeedFlagNid;
        yield return LibcInternalNeedFlagNid;
        yield return IsThreadedName;
        yield return StdoutName;
        yield return StderrName;
        yield return StdinName;
        yield return EnvironName;
    }

    public static void ConfigureProcessImageName(string? processImageName)
    {
        var effectiveName = string.IsNullOrWhiteSpace(processImageName)
            ? "eboot.bin"
            : processImageName;
        var encodedName = Encoding.UTF8.GetBytes(effectiveName);
        var byteCount = Math.Min(encodedName.Length, ProgNameMaxBytes);

        lock (_gate)
        {
            if (_progNameBufferAddress == 0 || _progNamePointerAddress == 0)
            {
                return;
            }

            for (var i = 0; i <= ProgNameMaxBytes; i++)
            {
                Marshal.WriteByte(_progNameBufferAddress, i, 0);
            }

            Marshal.Copy(encodedName, 0, _progNameBufferAddress, byteCount);
            WritePointer(_progNamePointerAddress, _progNameBufferAddress);
        }
    }

    public static bool TryGetAddress(string nid, out ulong address)
    {
        var pointer = nid switch
        {
            StackChkGuardNid => _stackChkGuardAddress,
            ProgNameNid => _progNamePointerAddress,
            LibcNeedFlagNid => _libcNeedFlagAddress,
            LibcInternalNeedFlagNid => _libcInternalNeedFlagAddress,
            IsThreadedName => _isThreadedAddress,
            StdoutName => _stdoutPointerAddress,
            StderrName => _stderrPointerAddress,
            StdinName => _stdinPointerAddress,
            EnvironName => _environAddress,
            _ => 0,
        };

        if (pointer == 0)
        {
            address = 0;
            return false;
        }

        address = unchecked((ulong)pointer);
        return true;
    }

    public static bool TryGetProcessArgv(out ulong address)
    {
        address = unchecked((ulong)_argvAddress);
        return _argvAddress != 0;
    }

    private static nint Allocate(int size)
    {
        try
        {
            var memory = Marshal.AllocHGlobal(size);
            for (var i = 0; i < size; i++)
            {
                Marshal.WriteByte(memory, i, 0);
            }

            return memory;
        }
        catch
        {
            return 0;
        }
    }

    private static void WritePointer(nint target, nint value)
    {
        if (nint.Size == sizeof(int))
        {
            Marshal.WriteInt32(target, value.ToInt32());
            return;
        }

        Marshal.WriteInt64(target, value.ToInt64());
    }

    private static void InitializeStreamPointer(nint pointerAddress)
    {
        if (pointerAddress != 0)
        {
            WritePointer(pointerAddress, pointerAddress);
        }
    }
}
