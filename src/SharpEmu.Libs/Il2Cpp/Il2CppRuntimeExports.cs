// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using SharpEmu.HLE;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Handlers for the subset of the ~220-function IL2CPP embedding API (il2cpp_init, il2cpp_alloc,
/// GC handles, monitors, thread attach, ...) that can be given real, correct semantics without a
/// full class-metadata-aware IL2CPP/Mono-style VM. Each method here is registered under a synthetic
/// "__il2cpp_dyn_*" NID with its real IL2CPP export name, resolved dynamically at runtime through
/// il2cpp_api_lookup_symbol (see DirectExecutionBackend.Imports.TryResolveOrCreateIl2CppSymbol) -
/// these are never present as fixed entries in the SELF import table. Symbols not implemented here
/// fall back to <see cref="Il2CppGenericStub"/>. See docs/game-notes/PPSA09804.md for the full
/// picture of what implementing the entire API would require.
/// </summary>
public static class Il2CppRuntimeExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Il2Cpp");

    // ---- Lifecycle / configuration -----------------------------------------------------------

    [SysAbiExport(Nid = "__il2cpp_dyn_init", ExportName = "il2cpp_init", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int Init(CpuContext ctx)
    {
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 256, out var domain);
        Log.Info($"il2cpp_init('{domain}')");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_init_utf16", ExportName = "il2cpp_init_utf16", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int InitUtf16(CpuContext ctx)
    {
        Log.Info("il2cpp_init_utf16(...)");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_shutdown", ExportName = "il2cpp_shutdown", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int Shutdown(CpuContext ctx)
    {
        Log.Info("il2cpp_shutdown()");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_config_dir", ExportName = "il2cpp_set_config_dir", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetConfigDir(CpuContext ctx) => LogDirSetter(ctx, "config_dir");

    [SysAbiExport(Nid = "__il2cpp_dyn_set_data_dir", ExportName = "il2cpp_set_data_dir", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetDataDir(CpuContext ctx) => LogDirSetter(ctx, "data_dir");

    [SysAbiExport(Nid = "__il2cpp_dyn_set_temp_dir", ExportName = "il2cpp_set_temp_dir", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetTempDir(CpuContext ctx) => LogDirSetter(ctx, "temp_dir");

    [SysAbiExport(Nid = "__il2cpp_dyn_set_config", ExportName = "il2cpp_set_config", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetConfig(CpuContext ctx) => LogDirSetter(ctx, "config");

    [SysAbiExport(Nid = "__il2cpp_dyn_set_config_utf16", ExportName = "il2cpp_set_config_utf16", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetConfigUtf16(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_commandline_arguments", ExportName = "il2cpp_set_commandline_arguments", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetCommandlineArguments(CpuContext ctx)
    {
        Log.Trace($"il2cpp_set_commandline_arguments(argc={ctx[CpuRegister.Rdi]})");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_commandline_arguments_utf16", ExportName = "il2cpp_set_commandline_arguments_utf16", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetCommandlineArgumentsUtf16(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_default_thread_affinity", ExportName = "il2cpp_set_default_thread_affinity", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetDefaultThreadAffinity(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_find_plugin_callback", ExportName = "il2cpp_set_find_plugin_callback", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetFindPluginCallback(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_set_memory_callbacks", ExportName = "il2cpp_set_memory_callbacks", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int SetMemoryCallbacks(CpuContext ctx)
    {
        // il2cpp_alloc/il2cpp_free below always use the host allocator directly; a guest-provided
        // override table is intentionally not honored.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_debugger_set_agent_options", ExportName = "il2cpp_debugger_set_agent_options", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int DebuggerSetAgentOptions(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_register_debugger_agent_transport", ExportName = "il2cpp_register_debugger_agent_transport", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RegisterDebuggerAgentTransport(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_is_debugger_attached", ExportName = "il2cpp_is_debugger_attached", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int IsDebuggerAttached(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_register_log_callback", ExportName = "il2cpp_register_log_callback", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RegisterLogCallback(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_unhandled_exception_policy_set", ExportName = "il2cpp_runtime_unhandled_exception_policy_set", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeUnhandledExceptionPolicySet(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_override_stack_backtrace", ExportName = "il2cpp_override_stack_backtrace", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int OverrideStackBacktrace(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_unity_install_unitytls_interface", ExportName = "il2cpp_unity_install_unitytls_interface", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int UnityInstallUnitytlsInterface(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static int LogDirSetter(CpuContext ctx, string label)
    {
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 512, out var path);
        Log.Info($"il2cpp_set_{label}('{path}')");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ---- Memory -------------------------------------------------------------------------------

    [SysAbiExport(Nid = "__il2cpp_dyn_alloc", ExportName = "il2cpp_alloc", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static unsafe int Alloc(CpuContext ctx)
    {
        var size = ctx[CpuRegister.Rdi];
        var ptr = size == 0 ? null : NativeMemory.Alloc((nuint)size);
        ctx[CpuRegister.Rax] = unchecked((ulong)ptr);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_free", ExportName = "il2cpp_free", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static unsafe int Free(CpuContext ctx)
    {
        var address = ctx[CpuRegister.Rdi];
        if (address != 0)
        {
            NativeMemory.Free((void*)address);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_allocation_granularity", ExportName = "il2cpp_allocation_granularity", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int AllocationGranularity(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 16;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ---- GC (no real managed heap here, so collection itself is always a no-op) ---------------

    private static int _gcDisabled;

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_collect", ExportName = "il2cpp_gc_collect", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcCollect(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_collect_a_little", ExportName = "il2cpp_gc_collect_a_little", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcCollectALittle(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_disable", ExportName = "il2cpp_gc_disable", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcDisable(CpuContext ctx)
    {
        Volatile.Write(ref _gcDisabled, 1);
        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_enable", ExportName = "il2cpp_gc_enable", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcEnable(CpuContext ctx)
    {
        Volatile.Write(ref _gcDisabled, 0);
        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_is_disabled", ExportName = "il2cpp_gc_is_disabled", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcIsDisabled(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Volatile.Read(ref _gcDisabled));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_is_incremental", ExportName = "il2cpp_gc_is_incremental", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcIsIncremental(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_get_heap_size", ExportName = "il2cpp_gc_get_heap_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcGetHeapSize(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_get_used_size", ExportName = "il2cpp_gc_get_used_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcGetUsedSize(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_get_max_time_slice_ns", ExportName = "il2cpp_gc_get_max_time_slice_ns", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcGetMaxTimeSliceNs(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_set_max_time_slice_ns", ExportName = "il2cpp_gc_set_max_time_slice_ns", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcSetMaxTimeSliceNs(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_has_strict_wbarriers", ExportName = "il2cpp_gc_has_strict_wbarriers", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHasStrictWbarriers(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_set_external_allocation_tracker", ExportName = "il2cpp_gc_set_external_allocation_tracker", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcSetExternalAllocationTracker(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_set_external_wbarrier_tracker", ExportName = "il2cpp_gc_set_external_wbarrier_tracker", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcSetExternalWbarrierTracker(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_wbarrier_set_field", ExportName = "il2cpp_gc_wbarrier_set_field", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcWbarrierSetField(CpuContext ctx)
    {
        // void il2cpp_gc_wbarrier_set_field(Il2CppObject* obj, void** targetAddress, void* value)
        // There is no moving/compacting GC on the host side, so the write barrier degenerates to a
        // plain pointer store - which is the actually correct operation to perform here.
        var targetAddress = ctx[CpuRegister.Rsi];
        var value = ctx[CpuRegister.Rdx];
        if (targetAddress != 0)
        {
            ctx.TryWriteUInt64(targetAddress, value);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gc_foreach_heap", ExportName = "il2cpp_gc_foreach_heap", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcForeachHeap(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_start_gc_world", ExportName = "il2cpp_start_gc_world", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StartGcWorld(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_stop_gc_world", ExportName = "il2cpp_stop_gc_world", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StopGcWorld(CpuContext ctx) => Neutral(ctx);

    // ---- GC handles (identity mapping: no compacting GC means the object pointer is already a
    // stable handle, so gchandle_new/get_target/free can pass it straight through) ---------------

    [SysAbiExport(Nid = "__il2cpp_dyn_gchandle_new", ExportName = "il2cpp_gchandle_new", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHandleNew(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = ctx[CpuRegister.Rdi];
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gchandle_new_weakref", ExportName = "il2cpp_gchandle_new_weakref", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHandleNewWeakref(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = ctx[CpuRegister.Rdi];
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gchandle_get_target", ExportName = "il2cpp_gchandle_get_target", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHandleGetTarget(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = ctx[CpuRegister.Rdi];
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_gchandle_free", ExportName = "il2cpp_gchandle_free", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHandleFree(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_gchandle_foreach_get_target", ExportName = "il2cpp_gchandle_foreach_get_target", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GcHandleForeachGetTarget(CpuContext ctx) => Neutral(ctx);

    // ---- Threading ------------------------------------------------------------------------------
    // il2cpp_thread_attach/current hand the guest an opaque "thread" pointer. Rather than an
    // arbitrary non-dereferenceable value, each host OS thread gets a small zeroed native buffer so
    // that if guest code reads a field out of it (expecting an Il2CppThread-shaped struct), it reads
    // zero instead of faulting.

    private const int ThreadHandleBufferSize = 256;

    private static readonly ThreadLocal<nint> _threadHandles = new(static () => AllocateThreadHandle());

    private static unsafe nint AllocateThreadHandle() => unchecked((nint)NativeMemory.AllocZeroed(ThreadHandleBufferSize));

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_attach", ExportName = "il2cpp_thread_attach", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadAttach(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)_threadHandles.Value);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_detach", ExportName = "il2cpp_thread_detach", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadDetach(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_current", ExportName = "il2cpp_thread_current", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadCurrent(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)_threadHandles.Value);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_is_vm_thread", ExportName = "il2cpp_is_vm_thread", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int IsVmThread(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 1;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_get_all_attached_threads", ExportName = "il2cpp_thread_get_all_attached_threads", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadGetAllAttachedThreads(CpuContext ctx)
    {
        var sizeAddress = ctx[CpuRegister.Rdi];
        if (sizeAddress != 0)
        {
            ctx.TryWriteUInt64(sizeAddress, 0);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_get_stack_depth", ExportName = "il2cpp_thread_get_stack_depth", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadGetStackDepth(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_get_top_frame", ExportName = "il2cpp_thread_get_top_frame", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadGetTopFrame(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_get_frame_at", ExportName = "il2cpp_thread_get_frame_at", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadGetFrameAt(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_thread_walk_frame_stack", ExportName = "il2cpp_thread_walk_frame_stack", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ThreadWalkFrameStack(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_current_thread_get_stack_depth", ExportName = "il2cpp_current_thread_get_stack_depth", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CurrentThreadGetStackDepth(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_current_thread_get_top_frame", ExportName = "il2cpp_current_thread_get_top_frame", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CurrentThreadGetTopFrame(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_current_thread_get_frame_at", ExportName = "il2cpp_current_thread_get_frame_at", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CurrentThreadGetFrameAt(CpuContext ctx) => Neutral(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_current_thread_walk_frame_stack", ExportName = "il2cpp_current_thread_walk_frame_stack", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CurrentThreadWalkFrameStack(CpuContext ctx) => Neutral(ctx);

    // ---- Monitors (genuine mutual exclusion via .NET Monitor, keyed by guest object pointer) ----

    private static readonly ConcurrentDictionary<ulong, object> _monitors = new();

    private static object GetMonitor(ulong obj) => _monitors.GetOrAdd(obj, static _ => new object());

    // Bounded like monitor_wait below: with no real guest scheduler guaranteeing the thread holding
    // a lock ever reaches its matching monitor_exit, an unbounded Enter can stall the process for a
    // long time (observed in practice: a real-thread guest worker blocked here while the thread
    // that would have released it was itself stuck on an unrelated unimplemented import). The
    // timeout is kept short (tens of milliseconds, not seconds) because guest code commonly takes
    // several locks in a row - a multi-second timeout on each one compounds into a very long,
    // CPU-idle stall before the process ever gets back to doing useful work.
    private static readonly TimeSpan MonitorEnterTimeout = TimeSpan.FromMilliseconds(50);

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_enter", ExportName = "il2cpp_monitor_enter", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorEnter(CpuContext ctx)
    {
        var obj = ctx[CpuRegister.Rdi];
        if (!Monitor.TryEnter(GetMonitor(obj), MonitorEnterTimeout))
        {
            Log.Warning($"il2cpp_monitor_enter(0x{obj:X16}) timed out after {MonitorEnterTimeout.TotalMilliseconds}ms; proceeding without the lock.");
        }

        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_exit", ExportName = "il2cpp_monitor_exit", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorExit(CpuContext ctx)
    {
        TryMonitorOp(ctx[CpuRegister.Rdi], static m => Monitor.Exit(m));
        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_try_enter", ExportName = "il2cpp_monitor_try_enter", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorTryEnter(CpuContext ctx)
    {
        var timeoutMs = unchecked((int)ctx[CpuRegister.Rsi]);
        var acquired = Monitor.TryEnter(GetMonitor(ctx[CpuRegister.Rdi]), Math.Max(0, timeoutMs));
        ctx[CpuRegister.Rax] = acquired ? 1u : 0u;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_pulse", ExportName = "il2cpp_monitor_pulse", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorPulse(CpuContext ctx)
    {
        TryMonitorOp(ctx[CpuRegister.Rdi], static m => Monitor.Pulse(m));
        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_pulse_all", ExportName = "il2cpp_monitor_pulse_all", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorPulseAll(CpuContext ctx)
    {
        TryMonitorOp(ctx[CpuRegister.Rdi], static m => Monitor.PulseAll(m));
        return Neutral(ctx);
    }

    // See MonitorEnterTimeout above for why this is short rather than a "generous" multi-second
    // wait: repeated waits on the boot path otherwise compound into a very long, CPU-idle stall.
    private static readonly TimeSpan MonitorWaitTimeout = TimeSpan.FromMilliseconds(50);

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_wait", ExportName = "il2cpp_monitor_wait", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorWait(CpuContext ctx)
    {
        var obj = ctx[CpuRegister.Rdi];
        TryMonitorOp(obj, m =>
        {
            if (!Monitor.Wait(m, MonitorWaitTimeout))
            {
                Log.Warning($"il2cpp_monitor_wait(0x{obj:X16}) timed out after {MonitorWaitTimeout.TotalMilliseconds}ms without a pulse; returning anyway.");
            }
        });
        return Neutral(ctx);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_monitor_try_wait", ExportName = "il2cpp_monitor_try_wait", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MonitorTryWait(CpuContext ctx)
    {
        var timeoutMs = unchecked((int)ctx[CpuRegister.Rsi]);
        var signaled = false;
        TryMonitorOp(ctx[CpuRegister.Rdi], m => signaled = Monitor.Wait(m, Math.Max(0, timeoutMs)));
        ctx[CpuRegister.Rax] = signaled ? 1u : 0u;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static void TryMonitorOp(ulong obj, Action<object> operation)
    {
        if (!_monitors.TryGetValue(obj, out var monitor))
        {
            // Guest called exit/pulse/wait without ever entering on this object from our side;
            // nothing to synchronize on, so there is nothing correct to do but ignore it.
            return;
        }

        try
        {
            operation(monitor);
        }
        catch (SynchronizationLockException ex)
        {
            Log.Warning($"il2cpp monitor op on 0x{obj:X16} failed: {ex.Message}");
        }
    }

    private static int Neutral(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }
}
