// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SharpEmu.HLE;

namespace SharpEmu.Core.Runtime;

/// <summary>
/// Renders a triage of the <c>libKernel</c> exports: every function grouped by bring-up priority area and
/// tagged with a severity (BLOCKER / CRITICAL / VISIBLE / COSMETIC / UNKNOWN). The output is deterministic so
/// a regenerated report only diffs when coverage or triage changes. It is a map for prioritising kernel work
/// on the titles that already boot rather than a claim of completeness.
/// </summary>
public static class KernelHleStatusReport
{
    private const string KernelLibrary = "libKernel";

    private static readonly KernelPriorityArea[] AreaOrder =
    [
        KernelPriorityArea.ThreadLifecycle,
        KernelPriorityArea.Synchronization,
        KernelPriorityArea.EventQueue,
        KernelPriorityArea.MemoryMapping,
        KernelPriorityArea.ModuleLoading,
        KernelPriorityArea.FileDescriptors,
        KernelPriorityArea.TimeClockSleep,
        KernelPriorityArea.ProcessParams,
        KernelPriorityArea.Other,
    ];

    private static readonly KernelFunctionSeverity[] SeverityOrder =
    [
        KernelFunctionSeverity.Blocker,
        KernelFunctionSeverity.Critical,
        KernelFunctionSeverity.Visible,
        KernelFunctionSeverity.Cosmetic,
        KernelFunctionSeverity.Unknown,
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string RenderMarkdown(IEnumerable<ExportedFunction> exports)
    {
        var areas = Group(exports);
        var all = areas.SelectMany(area => area.Functions).ToArray();
        var curated = all.Count(function => function.Classification.Curated);

        var builder = new StringBuilder();
        void Line(string text = "") => builder.Append(text).Append('\n');

        Line("# Kernel HLE Status");
        Line();
        Line($"_{all.Length} `libKernel` exports. {curated} triaged, {all.Length - curated} not yet triaged._ " +
             "Areas are inferred from export names; severities are a curated triage seed. Regenerate with " +
             "`SharpEmu --kernel-status`.");
        Line();
        Line("Severity: **BLOCKER** prevents boot; **CRITICAL** crash/deadlock; **VISIBLE** graphics/audio/input; " +
             "**COSMETIC** minor; **UNKNOWN** not yet triaged.");
        Line();
        Line("## Summary by area");
        Line();
        Line("| # | Area | Exports | BLOCKER | CRITICAL | VISIBLE | COSMETIC | UNKNOWN |");
        Line("| - | ---- | ------: | ------: | -------: | ------: | -------: | ------: |");
        foreach (var area in areas)
        {
            var priority = area.Area == KernelPriorityArea.Other ? "-" : ((int)area.Area).ToString();
            Line($"| {priority} | {KernelHleClassification.DescribeArea(area.Area)} | {area.Functions.Count} | " +
                 $"{area.Count(KernelFunctionSeverity.Blocker)} | {area.Count(KernelFunctionSeverity.Critical)} | " +
                 $"{area.Count(KernelFunctionSeverity.Visible)} | {area.Count(KernelFunctionSeverity.Cosmetic)} | " +
                 $"{area.Count(KernelFunctionSeverity.Unknown)} |");
        }

        foreach (var area in areas)
        {
            var priority = area.Area == KernelPriorityArea.Other ? string.Empty : $"{(int)area.Area}. ";
            Line();
            Line($"## {priority}{KernelHleClassification.DescribeArea(area.Area)} ({area.Functions.Count})");
            Line();
            Line("| Export | NID | Severity | Note |");
            Line("| ------ | --- | -------- | ---- |");
            foreach (var function in area.Functions)
            {
                Line($"| {function.Name} | `{function.Nid}` | {Format(function.Classification.Severity)} | {function.Classification.Note} |");
            }
        }

        return builder.ToString();
    }

    public static string RenderJson(IEnumerable<ExportedFunction> exports)
    {
        var areas = Group(exports);
        var all = areas.SelectMany(area => area.Functions).ToArray();
        var payload = new ReportDto(
            all.Length,
            all.Count(function => function.Classification.Curated),
            areas
                .Select(area => new AreaDto(
                    area.Area == KernelPriorityArea.Other ? 0 : (int)area.Area,
                    KernelHleClassification.DescribeArea(area.Area),
                    area.Functions.Count,
                    SeverityOrder.ToDictionary(Format, area.Count),
                    area.Functions
                        .Select(function => new FunctionDto(
                            function.Name,
                            function.Nid,
                            Format(function.Classification.Severity),
                            function.Classification.Curated,
                            function.Classification.Note))
                        .ToArray()))
                .ToArray());
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static List<AreaGroup> Group(IEnumerable<ExportedFunction> exports)
    {
        var functions = exports
            .Where(export => string.Equals(export.LibraryName, KernelLibrary, System.StringComparison.Ordinal))
            .Select(export => new ClassifiedFunction(export.Name, export.Nid, KernelHleClassification.Classify(export.Name)))
            .ToArray();

        var byArea = functions.ToLookup(function => function.Classification.Area);
        var groups = new List<AreaGroup>();
        foreach (var area in AreaOrder)
        {
            var ordered = byArea[area]
                .OrderBy(function => System.Array.IndexOf(SeverityOrder, function.Classification.Severity))
                .ThenBy(function => function.Name, System.StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length != 0)
            {
                groups.Add(new AreaGroup(area, ordered));
            }
        }

        return groups;
    }

    private static string Format(KernelFunctionSeverity severity) => severity.ToString().ToUpperInvariant();

    private sealed record ClassifiedFunction(string Name, string Nid, KernelFunctionClassification Classification);

    private sealed class AreaGroup(KernelPriorityArea area, IReadOnlyList<ClassifiedFunction> functions)
    {
        public KernelPriorityArea Area { get; } = area;

        public IReadOnlyList<ClassifiedFunction> Functions { get; } = functions;

        public int Count(KernelFunctionSeverity severity) =>
            Functions.Count(function => function.Classification.Severity == severity);
    }

    private sealed record ReportDto(int TotalExports, int CuratedCount, IReadOnlyList<AreaDto> Areas);

    private sealed record AreaDto(
        int Priority,
        string Area,
        int ExportCount,
        IReadOnlyDictionary<string, int> SeverityCounts,
        IReadOnlyList<FunctionDto> Exports);

    private sealed record FunctionDto(string Name, string Nid, string Severity, bool Curated, string Note);
}
