// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.Core.Memory;
using SharpEmu.Core.Runtime;
using SharpEmu.HLE;
using SharpEmu.Libs.Np;
using Xunit;

namespace SharpEmu.Tests;

public sealed class NpManagerExportsTests
{
    private const ulong ScratchAddress = 0x10000;
    private const int SceNpErrorSignedOut = unchecked((int)0x80550006);

    [Theory]
    [InlineData("account-id")]
    [InlineData("account-id-a")]
    [InlineData("np-id")]
    [InlineData("country")]
    [InlineData("country-a")]
    [InlineData("reachability")]
    public void OfflineAccountGetters_ReturnSignedOutWithoutWritingGuestMemory(string getter)
    {
        var memory = new VirtualMemory();
        memory.Map(
            ScratchAddress,
            0x1000,
            0,
            ReadOnlySpan<byte>.Empty,
            ProgramHeaderFlags.Read | ProgramHeaderFlags.Write);

        var sentinel = Enumerable.Repeat((byte)0xA5, 0x1000).ToArray();
        Assert.True(memory.TryWrite(ScratchAddress, sentinel));

        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = ScratchAddress + 0x800;
        context[CpuRegister.Rdx] = ScratchAddress + 0x900;
        context[CpuRegister.Rcx] = ScratchAddress + 0xA00;

        var result = getter switch
        {
            "account-id" => NpManagerExports.NpGetAccountId(context),
            "account-id-a" => NpManagerExports.NpGetAccountIdA(context),
            "np-id" => NpManagerExports.NpGetNpId(context),
            "country" => NpManagerExports.NpGetAccountCountry(context),
            "country-a" => NpManagerExports.NpGetAccountCountryA(context),
            "reachability" => NpManagerExports.NpGetNpReachabilityState(context),
            _ => throw new ArgumentOutOfRangeException(nameof(getter)),
        };

        Assert.Equal(SceNpErrorSignedOut, result);
        Assert.Equal(unchecked((ulong)SceNpErrorSignedOut), context[CpuRegister.Rax]);

        var actual = new byte[sentinel.Length];
        Assert.True(memory.TryRead(ScratchAddress, actual));
        Assert.Equal(sentinel, actual);
    }

    [Theory]
    [InlineData("a8R9-75u4iM", "sceNpGetAccountId")]
    [InlineData("rbknaUjpqWo", "sceNpGetAccountIdA")]
    [InlineData("p-o74CnoNzY", "sceNpGetNpId")]
    [InlineData("Ghz9iWDUtC4", "sceNpGetAccountCountry")]
    [InlineData("JT+t00a3TxA", "sceNpGetAccountCountryA")]
    [InlineData("e-ZuhGEoeC4", "sceNpGetNpReachabilityState")]
    public void DefaultCatalog_MapsNpAccountNidsToTheirExactNames(string nid, string name)
    {
        var export = Assert.Single(
            HleModuleCatalog.GetRegisteredExports(),
            candidate => candidate.Nid == nid);

        Assert.Equal(name, export.Name);
    }
}
