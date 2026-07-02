// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text.Json;
using SharpEmu.Core.Runtime;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using Xunit;

namespace SharpEmu.Tests;

public sealed class ExportCoverageReportTests
{
    private static ExportedFunction Export(string library, string name, string nid, Generation target)
        => new(library, nid, name, target, _ => 0);

    private static readonly ExportedFunction[] Sample =
    [
        Export("libSceVideoOut", "sceVideoOutOpen", "Up36PTk687E", Generation.Gen4 | Generation.Gen5),
        Export("libKernel", "sceKernelOpen", "1G3lF1Gg1k8", Generation.Gen4 | Generation.Gen5),
        Export("libSceAgc", "sceAgcCbNop", "LtTouSCZjHM", Generation.Gen5),
    ];

    [Fact]
    public void RenderMarkdown_GroupsByModule_WithSummaryAndPerModuleTables()
    {
        var markdown = ExportCoverageReport.RenderMarkdown(Sample);

        Assert.Contains("# HLE Export Coverage", markdown);
        Assert.Contains("3 exports across 3 modules", markdown);
        Assert.Contains("| libKernel | 1 | 1 | 1 |", markdown);
        Assert.Contains("| libSceAgc | 1 | 0 | 1 |", markdown); // Gen5-only shows 0 Gen4
        Assert.Contains("## libKernel (1)", markdown);
        Assert.Contains("| sceKernelOpen | `1G3lF1Gg1k8` | Gen4, Gen5 |", markdown);
        Assert.Contains("| sceAgcCbNop | `LtTouSCZjHM` | Gen5 |", markdown);

        // Modules are ordered alphabetically.
        Assert.True(
            markdown.IndexOf("## libKernel", StringComparison.Ordinal) <
            markdown.IndexOf("## libSceAgc", StringComparison.Ordinal));
        Assert.True(
            markdown.IndexOf("## libSceAgc", StringComparison.Ordinal) <
            markdown.IndexOf("## libSceVideoOut", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderMarkdown_IsDeterministic_RegardlessOfInputOrder()
    {
        var reversed = Sample.Reverse().ToArray();
        Assert.Equal(ExportCoverageReport.RenderMarkdown(Sample), ExportCoverageReport.RenderMarkdown(reversed));
    }

    [Fact]
    public void RenderJson_IsValid_WithModulesAndTotals()
    {
        using var document = JsonDocument.Parse(ExportCoverageReport.RenderJson(Sample));

        Assert.Equal(3, document.RootElement.GetProperty("totalExports").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("moduleCount").GetInt32());

        var firstModule = document.RootElement.GetProperty("modules").EnumerateArray().First();
        Assert.Equal("libKernel", firstModule.GetProperty("module").GetString());
        var firstExport = firstModule.GetProperty("exports").EnumerateArray().First();
        Assert.Equal("sceKernelOpen", firstExport.GetProperty("name").GetString());
        Assert.Equal("Gen4, Gen5", firstExport.GetProperty("target").GetString());
    }

    [Fact]
    public void DefaultCatalog_ExposesRegisteredExports_AcrossManyModules()
    {
        var exports = HleModuleCatalog.GetRegisteredExports();

        Assert.NotEmpty(exports);
        Assert.Contains(exports, export => export.Nid == "Up36PTk687E" && export.Name == "sceVideoOutOpen");
        Assert.True(exports.Select(export => export.LibraryName).Distinct(StringComparer.Ordinal).Count() > 5);
    }

    [Fact]
    public void ModuleManager_GetRegisteredExports_MatchesRegistrationCount()
    {
        var manager = new ModuleManager();
        var count = manager.RegisterFromAssembly(
            typeof(KernelExports).Assembly,
            Generation.Gen4 | Generation.Gen5,
            Aerolib.Instance);
        manager.Freeze();

        var exports = manager.GetRegisteredExports();

        Assert.Equal(count, exports.Count);
        Assert.All(exports, export => Assert.True(manager.TryGetExport(export.Nid, out _)));
    }
}
