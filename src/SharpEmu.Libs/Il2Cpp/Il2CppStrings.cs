// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text;
using SharpEmu.HLE;

namespace SharpEmu.Libs.Il2Cpp;

internal static class Il2CppStrings
{
    public static bool TryReadAscii(CpuContext ctx, ulong address, int maxLength, out string value)
    {
        value = string.Empty;
        if (address == 0 || maxLength <= 0)
        {
            return false;
        }

        var buffer = new byte[maxLength];
        var readLength = 0;
        Span<byte> singleByte = stackalloc byte[1];
        for (; readLength < maxLength; readLength++)
        {
            if (!ctx.Memory.TryRead(address + (ulong)readLength, singleByte))
            {
                return false;
            }

            if (singleByte[0] == 0)
            {
                break;
            }

            buffer[readLength] = singleByte[0];
        }

        value = Encoding.UTF8.GetString(buffer, 0, readLength);
        return true;
    }
}
