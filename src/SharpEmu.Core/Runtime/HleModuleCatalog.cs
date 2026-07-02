// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Generic;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
using SharpEmu.Libs.VideoOut;

namespace SharpEmu.Core.Runtime;

/// <summary>
/// Single source of truth for which HLE export assemblies the runtime wires up. Both the live runtime
/// (<see cref="SharpEmuRuntime.CreateDefault"/>) and the export-coverage report register through here so
/// the report always reflects exactly what the emulator would dispatch.
/// </summary>
public static class HleModuleCatalog
{
    /// <summary>Creates a fully registered and frozen <see cref="ModuleManager"/> for both generations.</summary>
    public static ModuleManager CreateRegisteredModuleManager()
    {
        var moduleManager = new ModuleManager();
        const Generation generations = Generation.Gen4 | Generation.Gen5;
        moduleManager.RegisterFromAssembly(typeof(VideoOutExports).Assembly, generations, Aerolib.Instance);
        moduleManager.RegisterFromAssembly(typeof(KernelExports).Assembly, generations, Aerolib.Instance);
        moduleManager.Freeze();
        return moduleManager;
    }

    /// <summary>Returns every HLE export the runtime would register, grouped-ready for reporting.</summary>
    public static IReadOnlyList<ExportedFunction> GetRegisteredExports() =>
        CreateRegisteredModuleManager().GetRegisteredExports();
}
