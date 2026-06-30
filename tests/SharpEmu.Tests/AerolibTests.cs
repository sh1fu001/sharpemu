// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Linq;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Tests;

public sealed class AerolibTests
{
    private const string UnknownNid = "____not_a_real_nid____";

    [Fact]
    public void GetName_UnknownNid_ReturnsNidUnchanged()
    {
        Assert.Equal(UnknownNid, Aerolib.Instance.GetName(UnknownNid));
    }

    [Fact]
    public void TryGetName_UnknownNid_ReturnsFalse()
    {
        Assert.False(Aerolib.Instance.TryGetName(UnknownNid, out var name));
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void ContainsNid_UnknownOrEmpty_ReturnsFalse()
    {
        Assert.False(Aerolib.Instance.ContainsNid(UnknownNid));
        Assert.False(Aerolib.Instance.ContainsNid(string.Empty));
    }

    [Fact]
    public void Empty_Catalog_ResolvesNothing()
    {
        var empty = Aerolib.Empty;
        Assert.False(empty.TryGetByNid("anything", out _));
        Assert.False(empty.TryGetByExportName("anything", out _));
    }

    [Fact]
    public void KnownNid_RoundTripsThroughLookups()
    {
        var all = Aerolib.Instance.GetAllNidNames();
        if (all.Count == 0)
        {
            // No embedded symbol table available in this build; nothing to assert.
            return;
        }

        Assert.Equal(all.Count, Aerolib.Instance.Count);

        var (nid, expectedName) = all.First();
        Assert.True(Aerolib.Instance.ContainsNid(nid));
        Assert.True(Aerolib.Instance.TryGetName(nid, out var name));
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedName, Aerolib.Instance.GetName(nid));

        Assert.True(Aerolib.Instance.TryGetByNid(nid, out var symbol));
        Assert.Equal(expectedName, symbol.ExportName);
    }
}
