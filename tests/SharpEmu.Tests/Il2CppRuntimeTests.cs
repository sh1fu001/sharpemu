// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Il2Cpp;
using Xunit;

namespace SharpEmu.Tests;

public sealed class Il2CppRuntimeTests
{
    [Fact]
    public void ClassUserData_UsesNonHeaderSlotAndRoundTrips()
    {
        var runtime = Il2CppRuntime.Instance;
        var classHandle = runtime.GetArrayClass(
            runtime.GetOpaqueHandle("test:userdata-element"),
            rank: 1);
        var marker = unchecked((nint)0x1234_5678_9ABC_DEF0);

        Assert.True(Il2CppRuntime.ClassUserDataOffset >= 0x20);
        Assert.True(runtime.SetClassUserData(classHandle, marker));
        Assert.Equal(marker, runtime.GetClassUserData(classHandle));
    }

    [Fact]
    public void NewArray_ProducesStableLengthAndByteLength()
    {
        var runtime = Il2CppRuntime.Instance;
        var elementClass = runtime.GetOpaqueHandle("test:array-element");
        var arrayClass = runtime.GetArrayClass(elementClass, rank: 1);

        var array = runtime.NewArray(arrayClass, length: 3);

        Assert.NotEqual(0, arrayClass);
        Assert.NotEqual(0, array);
        Assert.Equal(3UL, runtime.GetArrayLength(array));
        Assert.Equal((ulong)(3 * nint.Size), runtime.GetArrayByteLength(array));
        Assert.Equal((nuint)nint.Size, runtime.GetArrayElementSize(arrayClass));
        Assert.Equal(0UL, runtime.GetArrayLength((nint)1));
    }
}
