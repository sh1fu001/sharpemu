// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SharpEmu.HLE;

namespace SharpEmu.Core.Runtime;

/// <summary>
/// Renders the set of registered HLE exports as a coverage report, grouped by module (library) and then
/// by export/NID. The output is intentionally deterministic (no timestamps) so a regenerated report only
/// diffs when the actual export coverage changes — which makes it a useful map when several people work on
/// different modules (e.g. Kernel vs VideoOut vs Agc) in parallel without stepping on each other.
/// </summary>
public static class ExportCoverageReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string RenderMarkdown(IEnumerable<ExportedFunction> exports)
    {
        var modules = GroupByModule(exports);
        var totalExports = modules.Sum(module => module.Exports.Count);

        var builder = new StringBuilder();
        void Line(string text = "") => builder.Append(text).Append('\n');

        Line("# HLE Export Coverage");
        Line();
        Line($"_{totalExports} exports across {modules.Count} modules._ Generated from the " +
             "`[SysAbiExport]` attributes the runtime registers; regenerate with `SharpEmu --export-report`.");
        Line();
        Line("## Summary");
        Line();
        Line("| Module | Exports | Gen4 | Gen5 |");
        Line("| --- | ---: | ---: | ---: |");
        foreach (var module in modules)
        {
            Line($"| {module.Module} | {module.Exports.Count} | {module.Gen4Count} | {module.Gen5Count} |");
        }

        foreach (var module in modules)
        {
            Line();
            Line($"## {module.Module} ({module.Exports.Count})");
            Line();
            Line("| Export | NID | Target |");
            Line("| --- | --- | --- |");
            foreach (var export in module.Exports)
            {
                Line($"| {export.Name} | `{export.Nid}` | {FormatTarget(export.Target)} |");
            }
        }

        return builder.ToString();
    }

    public static string RenderJson(IEnumerable<ExportedFunction> exports)
    {
        var modules = GroupByModule(exports);
        var payload = new ReportDto(
            modules.Sum(module => module.Exports.Count),
            modules.Count,
            modules
                .Select(module => new ModuleDto(
                    module.Module,
                    module.Exports.Count,
                    module.Gen4Count,
                    module.Gen5Count,
                    module.Exports
                        .Select(export => new ExportDto(export.Name, export.Nid, FormatTarget(export.Target)))
                        .ToArray()))
                .ToArray());
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static List<ModuleGroup> GroupByModule(IEnumerable<ExportedFunction> exports)
    {
        return exports
            .GroupBy(export => export.LibraryName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(export => export.Name, StringComparer.Ordinal)
                    .ThenBy(export => export.Nid, StringComparer.Ordinal)
                    .ToArray();
                return new ModuleGroup(
                    group.Key,
                    ordered,
                    ordered.Count(export => (export.Target & Generation.Gen4) != 0),
                    ordered.Count(export => (export.Target & Generation.Gen5) != 0));
            })
            .ToList();
    }

    private static string FormatTarget(Generation target)
    {
        var gen4 = (target & Generation.Gen4) != 0;
        var gen5 = (target & Generation.Gen5) != 0;
        return (gen4, gen5) switch
        {
            (true, true) => "Gen4, Gen5",
            (true, false) => "Gen4",
            (false, true) => "Gen5",
            _ => "None",
        };
    }

    private sealed record ModuleGroup(
        string Module,
        IReadOnlyList<ExportedFunction> Exports,
        int Gen4Count,
        int Gen5Count);

    private sealed record ReportDto(
        int TotalExports,
        int ModuleCount,
        IReadOnlyList<ModuleDto> Modules);

    private sealed record ModuleDto(
        string Module,
        int ExportCount,
        int Gen4Count,
        int Gen5Count,
        IReadOnlyList<ExportDto> Exports);

    private sealed record ExportDto(
        string Name,
        string Nid,
        string Target);
}
