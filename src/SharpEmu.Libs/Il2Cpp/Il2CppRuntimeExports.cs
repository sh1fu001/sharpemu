// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using SharpEmu.HLE;
using SharpEmu.Libs.Kernel;
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
        EnsureRuntimeAttached(ctx);
        // The API returns Il2CppDomain*, not a boolean. Hand the guest a stable, readable domain
        // object; integer 1 is non-null but crashes as soon as the caller dereferences it.
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("domain:root"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // Adapter exposing bounds-checked guest reads to the code-registration scanner.
    private sealed class CpuMemoryReader : IIl2CppMemoryReader
    {
        private readonly ICpuMemory _memory;

        public CpuMemoryReader(ICpuMemory memory) => _memory = memory;

        public bool TryReadBytes(ulong address, Span<byte> buffer) => _memory.TryRead(address, buffer);

        public bool TryWriteBytes(ulong address, ReadOnlySpan<byte> buffer) => _memory.TryWrite(address, buffer);
    }

    // Locates the binary's Il2CppMetadataRegistration (instance sizes + field offsets) once guest
    // memory and the main module range are both reachable. Idempotent; called from the il2cpp entry
    // points the game reaches earliest.
    private static void EnsureRuntimeAttached(CpuContext ctx)
    {
        if (Il2CppRuntime.Instance.CodeRegistrationAvailable)
        {
            return;
        }

        // Unity keeps generated methods and metadata-registration tables in
        // Il2CppUserAssemblies.prx on PS5. Older layouts may keep them in the main executable.
        if (!KernelModuleRegistry.TryFindByPathOrName("Il2CppUserAssemblies.prx", out var module) &&
            !KernelModuleRegistry.TryGetMainModule(out module))
        {
            return;
        }

        Il2CppRuntime.Instance.Attach(new CpuMemoryReader(ctx.Memory), module.BaseAddress, module.EndAddress);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_init_utf16", ExportName = "il2cpp_init_utf16", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int InitUtf16(CpuContext ctx)
    {
        Log.Info("il2cpp_init_utf16(...)");
        EnsureRuntimeAttached(ctx);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("domain:root"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ---- Runtime object graph (domain / corlib / assembly / image) ---------------------------
    // Walked immediately after il2cpp_init. Each returns a stable non-null opaque handle so the game
    // accepts init as successful and keeps a token to pass back into name-based il2cpp_* calls.

    [SysAbiExport(Nid = "__il2cpp_dyn_domain_get", ExportName = "il2cpp_domain_get", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int DomainGet(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("domain:root"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_get_corlib", ExportName = "il2cpp_get_corlib", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GetCorlib(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("image:corlib"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_domain_assembly_open", ExportName = "il2cpp_domain_assembly_open", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int DomainAssemblyOpen(CpuContext ctx)
    {
        // Il2CppAssembly* il2cpp_domain_assembly_open(Il2CppDomain* domain, const char* name)
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var name);
        var key = string.IsNullOrEmpty(name) ? "assembly:default" : "assembly:" + name;
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle(key));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_assembly_get_image", ExportName = "il2cpp_assembly_get_image", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int AssemblyGetImage(CpuContext ctx)
    {
        // Il2CppImage* il2cpp_assembly_get_image(const Il2CppAssembly* assembly). Hand back a stable
        // image token; class lookups resolve by namespace/name rather than by image identity.
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("image:default"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_image", ExportName = "il2cpp_class_get_image", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetImage(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("image:default"));
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

    // ---- Internal-call (icall) registry ------------------------------------------------------
    // The game registers native implementations for its managed internal calls at startup and later
    // invokes them through the resolved pointer. Left as no-op stubs, resolution returned 0/garbage
    // and the game crashed calling through it; a real name->pointer registry fixes that.

    [SysAbiExport(Nid = "__il2cpp_dyn_add_internal_call", ExportName = "il2cpp_add_internal_call", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int AddInternalCall(CpuContext ctx)
    {
        // void il2cpp_add_internal_call(const char* name, Il2CppMethodPointer method)
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 1024, out var name);
        Il2CppRuntime.Instance.RegisterInternalCall(name, unchecked((nint)ctx[CpuRegister.Rsi]));
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_resolve_icall", ExportName = "il2cpp_resolve_icall", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ResolveIcall(CpuContext ctx)
    {
        // Il2CppMethodPointer il2cpp_resolve_icall(const char* name)
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 1024, out var name);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.ResolveInternalCall(name));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ---- Metadata-backed class identity + real string/array objects --------------------------
    // These are backed by the parsed global-metadata.dat (see Il2CppRuntime). They return real,
    // dereferenceable data - crucially, String objects with a correct length field - instead of the
    // generic 0 stub, which is what the compiled game reads inline during startup.

    [SysAbiExport(Nid = "__il2cpp_dyn_class_from_name", ExportName = "il2cpp_class_from_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassFromName(CpuContext ctx)
    {
        // Il2CppClass* il2cpp_class_from_name(Il2CppImage*, const char* namespaze, const char* name)
        EnsureRuntimeAttached(ctx);
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var namespaze);
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdx], 512, out var name);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetClassFromName(namespaze, name));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_instance_size", ExportName = "il2cpp_class_instance_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassInstanceSize(CpuContext ctx)
    {
        // uint32_t il2cpp_class_instance_size(Il2CppClass* klass)
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetInstanceSize(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_field_from_name", ExportName = "il2cpp_class_get_field_from_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetFieldFromName(CpuContext ctx)
    {
        // FieldInfo* il2cpp_class_get_field_from_name(Il2CppClass* klass, const char* name)
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var name);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetFieldFromName(unchecked((nint)ctx[CpuRegister.Rdi]), name));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_offset", ExportName = "il2cpp_field_get_offset", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetOffset(CpuContext ctx)
    {
        // size_t il2cpp_field_get_offset(FieldInfo* field)
        var offset = Il2CppRuntime.Instance.GetFieldOffset(unchecked((nint)ctx[CpuRegister.Rdi]));
        ctx[CpuRegister.Rax] = offset < 0 ? 0UL : unchecked((ulong)(uint)offset);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_methods", ExportName = "il2cpp_class_get_methods", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetMethods(CpuContext ctx)
    {
        // const MethodInfo* il2cpp_class_get_methods(Il2CppClass*, void** iter)
        var iteratorAddress = ctx[CpuRegister.Rsi];
        var ordinal = 0UL;
        if (iteratorAddress == 0 ||
            !ctx.TryReadUInt64(iteratorAddress, out ordinal) ||
            ordinal > int.MaxValue)
        {
            ctx[CpuRegister.Rax] = 0;
            return (int)OrbisGen2Result.ORBIS_GEN2_OK;
        }

        var method = Il2CppRuntime.Instance.GetMethodAtOrdinal(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            (int)ordinal);
        if (method != 0)
        {
            _ = ctx.TryWriteUInt64(iteratorAddress, ordinal + 1);
        }

        ctx[CpuRegister.Rax] = unchecked((ulong)method);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_method_from_name", ExportName = "il2cpp_class_get_method_from_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetMethodFromName(CpuContext ctx)
    {
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var name);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetMethodFromName(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            name,
            unchecked((int)ctx[CpuRegister.Rdx])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_num_methods", ExportName = "il2cpp_class_num_methods", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassNumMethods(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((uint)Il2CppRuntime.Instance.GetClassMethodCount(
            unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_name", ExportName = "il2cpp_method_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetName(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetMethodName(
            unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_class", ExportName = "il2cpp_method_get_class", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetClass(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetMethodClass(
            unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_token", ExportName = "il2cpp_method_get_token", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetToken(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetMethodToken(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_param_count", ExportName = "il2cpp_method_get_param_count", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetParameterCount(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetMethodParameterCount(
            unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_flags", ExportName = "il2cpp_method_get_flags", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetFlags(CpuContext ctx)
    {
        var flags = Il2CppRuntime.Instance.GetMethodFlags(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            out var implementationFlags);
        if (ctx[CpuRegister.Rsi] != 0)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer, implementationFlags);
            _ = ctx.Memory.TryWrite(ctx[CpuRegister.Rsi], buffer);
        }

        ctx[CpuRegister.Rax] = flags;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_invoke", ExportName = "il2cpp_runtime_invoke", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeInvoke(CpuContext ctx)
    {
        // Invocation is intentionally deferred until method pointers have been validated. Preserve
        // the API contract by clearing Il2CppException** and returning a null result object.
        if (ctx[CpuRegister.Rcx] != 0)
        {
            _ = ctx.TryWriteUInt64(ctx[CpuRegister.Rcx], 0);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_name", ExportName = "il2cpp_class_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetName(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetClassName(unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_namespace", ExportName = "il2cpp_class_get_namespace", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetNamespace(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetClassNamespace(unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_assemblyname", ExportName = "il2cpp_class_get_assemblyname", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetAssemblyName(CpuContext ctx)
    {
        // A valid C string is required even when image-to-assembly mapping is not modeled yet;
        // Unity dereferences the first byte without a null check.
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetOpaqueHandle("cstring:assembly-name"));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_subclass_of", ExportName = "il2cpp_class_is_subclass_of", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsSubclassOf(CpuContext ctx)
    {
        // Identity is always a valid reflexive subclass result. Parent-chain traversal will be
        // added with method/type relationship metadata.
        ctx[CpuRegister.Rax] = ctx[CpuRegister.Rdi] != 0 &&
                               ctx[CpuRegister.Rdi] == ctx[CpuRegister.Rsi]
            ? 1UL
            : 0UL;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_flags", ExportName = "il2cpp_class_get_flags", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetFlags(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetClassFlags(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_abstract", ExportName = "il2cpp_class_is_abstract", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsAbstract(CpuContext ctx)
    {
        const uint TypeAttributesAbstract = 0x80;
        ctx[CpuRegister.Rax] =
            (Il2CppRuntime.Instance.GetClassFlags(unchecked((nint)ctx[CpuRegister.Rdi])) &
             TypeAttributesAbstract) != 0
                ? 1UL
                : 0UL;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ---- Objects and arrays ------------------------------------------------------------------

    [SysAbiExport(Nid = "__il2cpp_dyn_object_new", ExportName = "il2cpp_object_new", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ObjectNew(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewObject(unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_class_get", ExportName = "il2cpp_array_class_get", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayClassGet(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetArrayClass(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((uint)ctx[CpuRegister.Rsi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_bounded_array_class_get", ExportName = "il2cpp_bounded_array_class_get", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int BoundedArrayClassGet(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetArrayClass(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((uint)ctx[CpuRegister.Rsi]),
            bounded: true));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_new", ExportName = "il2cpp_array_new", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayNew(CpuContext ctx)
    {
        var arrayClass = Il2CppRuntime.Instance.GetArrayClass(unchecked((nint)ctx[CpuRegister.Rdi]), rank: 1);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewArray(arrayClass, ctx[CpuRegister.Rsi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_new_specific", ExportName = "il2cpp_array_new_specific", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayNewSpecific(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewArray(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            ctx[CpuRegister.Rsi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_length", ExportName = "il2cpp_array_length", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayLength(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetArrayLength(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_get_byte_length", ExportName = "il2cpp_array_get_byte_length", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayGetByteLength(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetArrayByteLength(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_array_element_size", ExportName = "il2cpp_array_element_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayElementSize(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = (ulong)Il2CppRuntime.Instance.GetArrayElementSize(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_array_element_size", ExportName = "il2cpp_class_array_element_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassArrayElementSize(CpuContext ctx) => ArrayElementSize(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_string_new", ExportName = "il2cpp_string_new", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringNew(CpuContext ctx)
    {
        // Il2CppString* il2cpp_string_new(const char* str) - UTF-8 input.
        EnsureRuntimeAttached(ctx);
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 0x4000, out var value);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewString(value));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_string_new_wrapper", ExportName = "il2cpp_string_new_wrapper", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringNewWrapper(CpuContext ctx)
    {
        // Il2CppString* il2cpp_string_new_wrapper(const char* str) - same contract as il2cpp_string_new.
        EnsureRuntimeAttached(ctx);
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdi], 0x4000, out var value);
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewString(value));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_string_new_len", ExportName = "il2cpp_string_new_len", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringNewLen(CpuContext ctx)
    {
        // Il2CppString* il2cpp_string_new_len(const char* str, uint32_t length) - UTF-8 input.
        var address = ctx[CpuRegister.Rdi];
        var length = (int)Math.Min(ctx[CpuRegister.Rsi], 0x40000);
        var utf8 = new byte[length];
        var value = ctx.Memory.TryRead(address, utf8)
            ? System.Text.Encoding.UTF8.GetString(utf8)
            : string.Empty;
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewString(value));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_string_new_utf16", ExportName = "il2cpp_string_new_utf16", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringNewUtf16(CpuContext ctx)
    {
        // Il2CppString* il2cpp_string_new_utf16(const uint16_t* text, int32_t len)
        var address = ctx[CpuRegister.Rdi];
        var length = (int)Math.Min(ctx[CpuRegister.Rsi], 0x40000);
        var bytes = new byte[length * sizeof(char)];
        var value = length > 0 && ctx.Memory.TryRead(address, bytes)
            ? System.Text.Encoding.Unicode.GetString(bytes)
            : string.Empty;
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.NewString(value));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_string_length", ExportName = "il2cpp_string_length", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringLength(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)(uint)Il2CppRuntime.Instance.GetStringLength(unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_string_chars", ExportName = "il2cpp_string_chars", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringChars(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.Instance.GetStringChars(unchecked((nint)ctx[CpuRegister.Rdi])));
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
