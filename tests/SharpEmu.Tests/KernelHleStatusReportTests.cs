// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text.Json;
using SharpEmu.Core.Runtime;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Tests;

public sealed class KernelHleStatusReportTests
{
    private static ExportedFunction Kernel(string name, string nid) =>
        new("libKernel", nid, name, Generation.Gen4 | Generation.Gen5, _ => 0);

    [Theory]
    [InlineData("scePthreadCreate", KernelPriorityArea.ThreadLifecycle)]
    [InlineData("pthread_create", KernelPriorityArea.ThreadLifecycle)]
    [InlineData("__tls_get_addr", KernelPriorityArea.ThreadLifecycle)]
    [InlineData("scePthreadMutexLock", KernelPriorityArea.Synchronization)]  // "pthread" but a mutex first
    [InlineData("scePthreadCondWait", KernelPriorityArea.Synchronization)]
    [InlineData("sem_wait", KernelPriorityArea.Synchronization)]
    [InlineData("sceKernelWaitSema", KernelPriorityArea.Synchronization)]
    [InlineData("sceKernelCreateEqueue", KernelPriorityArea.EventQueue)]
    [InlineData("sceKernelWaitEventFlag", KernelPriorityArea.EventQueue)]
    [InlineData("sceKernelMapNamedFlexibleMemory", KernelPriorityArea.MemoryMapping)]
    [InlineData("sceKernelMunmap", KernelPriorityArea.MemoryMapping)]
    [InlineData("sceKernelLoadStartModule", KernelPriorityArea.ModuleLoading)]
    [InlineData("sceKernelOpen", KernelPriorityArea.FileDescriptors)]
    [InlineData("read", KernelPriorityArea.FileDescriptors)]
    [InlineData("sceKernelClockGettime", KernelPriorityArea.TimeClockSleep)]
    [InlineData("sceKernelReadTsc", KernelPriorityArea.TimeClockSleep)]  // "read" but a clock first
    [InlineData("sceKernelUsleep", KernelPriorityArea.TimeClockSleep)]
    [InlineData("sceKernelGetProcParam", KernelPriorityArea.ProcessParams)]
    [InlineData("sceKernelUuidCreate", KernelPriorityArea.Other)]
    public void InferArea_MapsRepresentativeNames(string name, KernelPriorityArea expected)
        => Assert.Equal(expected, KernelHleClassification.InferArea(name));

    [Fact]
    public void Classify_CuratedFunction_HasConfidentSeverity()
    {
        var classification = KernelHleClassification.Classify("scePthreadCreate");
        Assert.Equal(KernelPriorityArea.ThreadLifecycle, classification.Area);
        Assert.Equal(KernelFunctionSeverity.Blocker, classification.Severity);
        Assert.True(classification.Curated);
    }

    [Fact]
    public void Classify_UncuratedFunction_IsUnknownAndNotCurated()
    {
        var classification = KernelHleClassification.Classify("sceKernelDebugRaiseException");
        Assert.Equal(KernelFunctionSeverity.Unknown, classification.Severity);
        Assert.False(classification.Curated);
    }

    [Fact]
    public void RenderMarkdown_GroupsByArea_AndExcludesNonKernelLibraries()
    {
        var exports = new[]
        {
            Kernel("scePthreadCreate", "n1"),
            Kernel("scePthreadMutexLock", "n2"),
            Kernel("sceKernelLoadStartModule", "n3"),
            new ExportedFunction("libSceVideoOut", "vo", "sceVideoOutOpen", Generation.Gen5, _ => 0),
        };

        var markdown = KernelHleStatusReport.RenderMarkdown(exports);

        Assert.Contains("# Kernel HLE Status", markdown);
        Assert.Contains("Thread lifecycle (pthread)", markdown);
        Assert.Contains("| scePthreadCreate | `n1` | BLOCKER |", markdown);
        Assert.Contains("| scePthreadMutexLock | `n2` | CRITICAL |", markdown);
        Assert.DoesNotContain("sceVideoOutOpen", markdown); // only libKernel is triaged
    }

    [Fact]
    public void RenderJson_IsValid_AndCountsOnlyKernelExports()
    {
        var exports = new[]
        {
            Kernel("scePthreadCreate", "n1"),
            Kernel("sceKernelLoadStartModule", "n2"),
            new ExportedFunction("libSceVideoOut", "vo", "sceVideoOutOpen", Generation.Gen5, _ => 0),
        };

        using var document = JsonDocument.Parse(KernelHleStatusReport.RenderJson(exports));

        Assert.Equal(2, document.RootElement.GetProperty("totalExports").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("curatedCount").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("areas").EnumerateArray());
    }

    [Fact]
    public void DefaultCatalog_KernelExports_AreAllClassifiedAndKnownBlockersCurated()
    {
        var kernelExports = HleModuleCatalog.GetRegisteredExports()
            .Where(export => export.LibraryName == "libKernel")
            .ToArray();

        Assert.NotEmpty(kernelExports);
        Assert.All(kernelExports, export => Assert.False(string.IsNullOrEmpty(KernelHleClassification.Classify(export.Name).Note)));
        Assert.Contains(kernelExports, export => export.Name == "scePthreadCreate");
        Assert.Equal(KernelFunctionSeverity.Blocker, KernelHleClassification.Classify("scePthreadCreate").Severity);
    }
}
