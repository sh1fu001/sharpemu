// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace SharpEmu.Core.Runtime;

/// <summary>The kernel subsystem an export belongs to, ordered by boot-bring-up priority.</summary>
public enum KernelPriorityArea
{
    ThreadLifecycle = 1,
    Synchronization = 2,
    EventQueue = 3,
    MemoryMapping = 4,
    ModuleLoading = 5,
    FileDescriptors = 6,
    TimeClockSleep = 7,
    ProcessParams = 8,

    /// <summary>Kernel export that does not fall into one of the eight priority areas yet.</summary>
    Other = 99,
}

/// <summary>How badly a missing or wrong export hurts a title that already boots.</summary>
public enum KernelFunctionSeverity
{
    /// <summary>Prevents the title from booting at all.</summary>
    Blocker,

    /// <summary>Causes a crash or a deadlock.</summary>
    Critical,

    /// <summary>Visible impact on graphics, audio or input.</summary>
    Visible,

    /// <summary>Log noise or otherwise minor behaviour.</summary>
    Cosmetic,

    /// <summary>Not yet triaged / understood.</summary>
    Unknown,
}

/// <summary>The triage verdict for a single kernel export.</summary>
public sealed record KernelFunctionClassification(
    KernelPriorityArea Area,
    KernelFunctionSeverity Severity,
    string Note,
    bool Curated);

/// <summary>
/// Classifies kernel (<c>libKernel</c>) exports into the eight bring-up priority areas and assigns a triage
/// severity. Areas are inferred from the export name; severities are a curated seed for the functions we are
/// confident about (the ones that matter for titles which already boot), and <see cref="KernelFunctionSeverity.Unknown"/>
/// for everything not yet triaged. The goal is to focus effort, not to label all 223 exports at once.
/// </summary>
public static class KernelHleClassification
{
    // Curated, confident verdicts keyed by exact export name. Everything else defaults to Unknown so the
    // report honestly separates "triaged" from "not yet understood".
    private static readonly Dictionary<string, (KernelFunctionSeverity Severity, string Note)> Curated =
        new(StringComparer.Ordinal)
        {
            // Thread lifecycle
            ["scePthreadCreate"] = (KernelFunctionSeverity.Blocker, "Guest worker threads never start without it."),
            ["pthread_create"] = (KernelFunctionSeverity.Blocker, "POSIX thread creation used by the C++ runtime."),
            ["pthread_create_name_np"] = (KernelFunctionSeverity.Blocker, "Named thread creation used during init."),
            ["__tls_get_addr"] = (KernelFunctionSeverity.Blocker, "Thread-local storage base, touched extremely early."),
            ["scePthreadJoin"] = (KernelFunctionSeverity.Critical, "A wrong join blocks the joining thread forever."),
            ["pthread_join"] = (KernelFunctionSeverity.Critical, "A wrong join blocks the joining thread forever."),
            ["scePthreadExit"] = (KernelFunctionSeverity.Critical, "Thread teardown; leaks or hangs if mismodelled."),
            ["scePthreadSelf"] = (KernelFunctionSeverity.Critical, "Identity used by locks and TLS."),
            ["pthread_self"] = (KernelFunctionSeverity.Critical, "Identity used by locks and TLS."),
            ["scePthreadOnce"] = (KernelFunctionSeverity.Critical, "One-time init guard; double-run corrupts state."),
            ["pthread_once"] = (KernelFunctionSeverity.Critical, "One-time init guard; double-run corrupts state."),
            ["scePthreadKeyCreate"] = (KernelFunctionSeverity.Critical, "TLS key allocation."),
            ["scePthreadGetspecific"] = (KernelFunctionSeverity.Critical, "TLS slot read."),
            ["scePthreadSetspecific"] = (KernelFunctionSeverity.Critical, "TLS slot write."),
            ["scePthreadGetname"] = (KernelFunctionSeverity.Cosmetic, "Debug thread name."),
            ["scePthreadGetprio"] = (KernelFunctionSeverity.Cosmetic, "Scheduling hint; safe to approximate."),
            ["scePthreadSetprio"] = (KernelFunctionSeverity.Cosmetic, "Scheduling hint; safe to approximate."),
            ["scePthreadSetaffinity"] = (KernelFunctionSeverity.Cosmetic, "Core affinity hint; safe to ignore."),
            ["scePthreadGetaffinity"] = (KernelFunctionSeverity.Cosmetic, "Core affinity hint; safe to approximate."),
            ["scePthreadYield"] = (KernelFunctionSeverity.Cosmetic, "Scheduling hint."),

            // Synchronization
            ["scePthreadMutexInit"] = (KernelFunctionSeverity.Blocker, "Mutexes are created pervasively during init."),
            ["pthread_mutex_init"] = (KernelFunctionSeverity.Blocker, "Mutexes are created pervasively during init."),
            ["scePthreadMutexLock"] = (KernelFunctionSeverity.Critical, "Incorrect locking deadlocks the guest."),
            ["scePthreadMutexUnlock"] = (KernelFunctionSeverity.Critical, "Missing unlock deadlocks waiters."),
            ["scePthreadMutexTrylock"] = (KernelFunctionSeverity.Critical, "Wrong result spins or deadlocks."),
            ["pthread_mutex_lock"] = (KernelFunctionSeverity.Critical, "Incorrect locking deadlocks the guest."),
            ["pthread_mutex_unlock"] = (KernelFunctionSeverity.Critical, "Missing unlock deadlocks waiters."),
            ["pthread_mutex_trylock"] = (KernelFunctionSeverity.Critical, "Wrong result spins or deadlocks."),
            ["scePthreadCondInit"] = (KernelFunctionSeverity.Critical, "Condition variable setup."),
            ["scePthreadCondWait"] = (KernelFunctionSeverity.Critical, "Missed wake-ups deadlock the waiter."),
            ["scePthreadCondTimedwait"] = (KernelFunctionSeverity.Critical, "Missed wake-ups / bad timeout deadlock or busy-loop."),
            ["scePthreadCondSignal"] = (KernelFunctionSeverity.Critical, "A dropped signal deadlocks the waiter."),
            ["scePthreadCondBroadcast"] = (KernelFunctionSeverity.Critical, "A dropped broadcast deadlocks waiters."),
            ["pthread_cond_wait"] = (KernelFunctionSeverity.Critical, "Missed wake-ups deadlock the waiter."),
            ["pthread_cond_signal"] = (KernelFunctionSeverity.Critical, "A dropped signal deadlocks the waiter."),
            ["pthread_cond_broadcast"] = (KernelFunctionSeverity.Critical, "A dropped broadcast deadlocks waiters."),
            ["sceKernelCreateSema"] = (KernelFunctionSeverity.Critical, "Semaphore object creation."),
            ["sceKernelWaitSema"] = (KernelFunctionSeverity.Critical, "Blocking wait; wrong count deadlocks."),
            ["sceKernelSignalSema"] = (KernelFunctionSeverity.Critical, "Missing signal deadlocks waiters."),
            ["sceKernelPollSema"] = (KernelFunctionSeverity.Critical, "Non-blocking acquire; wrong result mis-sequences."),
            ["sem_init"] = (KernelFunctionSeverity.Critical, "POSIX semaphore creation."),
            ["sem_wait"] = (KernelFunctionSeverity.Critical, "Blocking wait; wrong count deadlocks."),
            ["sem_post"] = (KernelFunctionSeverity.Critical, "Missing post deadlocks waiters."),

            // Event queue
            ["sceKernelCreateEqueue"] = (KernelFunctionSeverity.Critical, "Event queue used for flips, timers and I/O."),
            ["sceKernelWaitEqueue"] = (KernelFunctionSeverity.Critical, "Blocking wait; dropped events deadlock."),
            ["sceKernelDeleteEqueue"] = (KernelFunctionSeverity.Cosmetic, "Teardown."),
            ["sceKernelCreateEventFlag"] = (KernelFunctionSeverity.Critical, "Event-flag object creation."),
            ["sceKernelWaitEventFlag"] = (KernelFunctionSeverity.Critical, "Blocking wait; wrong bits deadlock."),
            ["sceKernelSetEventFlag"] = (KernelFunctionSeverity.Critical, "Missing set deadlocks waiters."),
            ["sceKernelAddUserEvent"] = (KernelFunctionSeverity.Visible, "User events drive frame/timer callbacks."),
            ["sceKernelTriggerUserEvent"] = (KernelFunctionSeverity.Visible, "Missing triggers stall dependent work."),
            ["sceKernelGetEventData"] = (KernelFunctionSeverity.Critical, "Event payload read after a wait."),
            ["sceKernelGetEventId"] = (KernelFunctionSeverity.Critical, "Event identity read after a wait."),

            // Memory mapping
            ["sceKernelAllocateDirectMemory"] = (KernelFunctionSeverity.Blocker, "Physical memory pool the title maps from."),
            ["sceKernelAllocateMainDirectMemory"] = (KernelFunctionSeverity.Blocker, "Main direct-memory pool allocation."),
            ["sceKernelMapDirectMemory"] = (KernelFunctionSeverity.Blocker, "Maps direct memory into the address space."),
            ["sceKernelMapNamedDirectMemory"] = (KernelFunctionSeverity.Blocker, "Maps named direct memory into the address space."),
            ["sceKernelMapFlexibleMemory"] = (KernelFunctionSeverity.Blocker, "Flexible (CPU) memory used for the guest heap."),
            ["sceKernelMapNamedFlexibleMemory"] = (KernelFunctionSeverity.Blocker, "Named flexible memory used for the guest heap."),
            ["sceKernelMunmap"] = (KernelFunctionSeverity.Critical, "Unmap; a wrong unmap frees live memory."),
            ["sceKernelMprotect"] = (KernelFunctionSeverity.Critical, "Protection change; wrong perms fault later."),
            ["sceKernelQueryMemoryProtection"] = (KernelFunctionSeverity.Visible, "Introspection used by allocators."),
            ["sceKernelVirtualQuery"] = (KernelFunctionSeverity.Visible, "Introspection used by allocators."),

            // Module loading
            ["sceKernelLoadStartModule"] = (KernelFunctionSeverity.Blocker, "Loads and starts the .prx modules the title needs."),
            ["sceKernelStopUnloadModule"] = (KernelFunctionSeverity.Cosmetic, "Module teardown."),
            ["sceKernelGetModuleInfo"] = (KernelFunctionSeverity.Cosmetic, "Introspection; often only used for logging/unwind."),
            ["sceKernelGetModuleList"] = (KernelFunctionSeverity.Cosmetic, "Introspection; often only used for logging/unwind."),

            // File descriptors
            ["open"] = (KernelFunctionSeverity.Visible, "File open; missing assets degrade content."),
            ["sceKernelOpen"] = (KernelFunctionSeverity.Visible, "File open; missing assets degrade content."),
            ["read"] = (KernelFunctionSeverity.Visible, "File read."),
            ["sceKernelRead"] = (KernelFunctionSeverity.Visible, "File read."),
            ["write"] = (KernelFunctionSeverity.Cosmetic, "File write; often logs/saves."),
            ["sceKernelWrite"] = (KernelFunctionSeverity.Cosmetic, "File write; often logs/saves."),
            ["close"] = (KernelFunctionSeverity.Cosmetic, "Descriptor teardown."),
            ["sceKernelClose"] = (KernelFunctionSeverity.Cosmetic, "Descriptor teardown."),

            // Time / clock / sleep
            ["sceKernelClockGettime"] = (KernelFunctionSeverity.Visible, "Monotonic/real clock; drives animation timing."),
            ["clock_gettime"] = (KernelFunctionSeverity.Visible, "Monotonic/real clock; drives animation timing."),
            ["sceKernelGettimeofday"] = (KernelFunctionSeverity.Visible, "Wall clock; drives timing."),
            ["gettimeofday"] = (KernelFunctionSeverity.Visible, "Wall clock; drives timing."),
            ["sceKernelGetProcessTimeCounter"] = (KernelFunctionSeverity.Visible, "High-resolution counter; pacing."),
            ["sceKernelReadTsc"] = (KernelFunctionSeverity.Visible, "Cycle counter; pacing."),
            ["sceKernelUsleep"] = (KernelFunctionSeverity.Visible, "Sleep pacing; wrong values stutter or busy-loop."),

            // Process params
            ["sceKernelGetProcParam"] = (KernelFunctionSeverity.Blocker, "Process parameters read during early runtime init."),
            ["sceKernelGetCompiledSdkVersion"] = (KernelFunctionSeverity.Cosmetic, "SDK version gate; usually just compared."),
            ["sceKernelIsNeoMode"] = (KernelFunctionSeverity.Cosmetic, "PS4 Pro / Neo mode query."),
        };

    public static KernelFunctionClassification Classify(string exportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        var area = InferArea(exportName);
        if (Curated.TryGetValue(exportName, out var curated))
        {
            return new KernelFunctionClassification(area, curated.Severity, curated.Note, Curated: true);
        }

        return new KernelFunctionClassification(area, KernelFunctionSeverity.Unknown, "Not yet triaged.", Curated: false);
    }

    public static KernelPriorityArea InferArea(string exportName)
    {
        var name = exportName.ToLowerInvariant();

        // Order matters: synchronization primitives contain "pthread" too, so match them before threads.
        if (ContainsAny(name, "mutex", "cond", "rwlock", "spinlock", "sema", "sem_"))
        {
            return KernelPriorityArea.Synchronization;
        }

        if (ContainsAny(name, "equeue", "kqueue", "event"))
        {
            return KernelPriorityArea.EventQueue;
        }

        if (ContainsAny(name, "memory", "mmap", "munmap", "mprotect", "mtypeprotect", "virtualquery",
                "reservevirtual", "batchmap", "prtaperture"))
        {
            return KernelPriorityArea.MemoryMapping;
        }

        if (ContainsAny(name, "module", "elf_phdr"))
        {
            return KernelPriorityArea.ModuleLoading;
        }

        if (ContainsAny(name, "clock", "time", "tsc", "sleep", "gettimeofday", "localtime", "utc"))
        {
            return KernelPriorityArea.TimeClockSleep;
        }

        if (ContainsAny(name, "pthread", "thread", "tls_get_addr", "atexit", "setspecific", "getspecific",
                "keycreate", "keydelete", "key_create", "key_delete", "setschedparam"))
        {
            return KernelPriorityArea.ThreadLifecycle;
        }

        if (ContainsAny(name, "getdent", "getdirentries", "opendir", "readdir", "closedir", "mkdir", "rmdir",
                "unlink", "lseek", "fstat") ||
            NameIn(name, "open", "_open", "close", "_close", "read", "_read", "write", "_write", "stat",
                "scekernelopen", "scekernelclose", "scekernelread", "scekernelwrite", "scekernellseek",
                "scekernelfstat", "scekernelstat"))
        {
            return KernelPriorityArea.FileDescriptors;
        }

        if (ContainsAny(name, "procparam", "getarg", "sdkversion", "neomode", "getgpi", "setgpo",
                "applicationheap"))
        {
            return KernelPriorityArea.ProcessParams;
        }

        return KernelPriorityArea.Other;
    }

    public static string DescribeArea(KernelPriorityArea area) => area switch
    {
        KernelPriorityArea.ThreadLifecycle => "Thread lifecycle (pthread)",
        KernelPriorityArea.Synchronization => "Synchronization (mutex / condvar / semaphore)",
        KernelPriorityArea.EventQueue => "Event queue & events",
        KernelPriorityArea.MemoryMapping => "Memory mapping",
        KernelPriorityArea.ModuleLoading => "Module loading",
        KernelPriorityArea.FileDescriptors => "File descriptors",
        KernelPriorityArea.TimeClockSleep => "Time / clock / sleep",
        KernelPriorityArea.ProcessParams => "Process params",
        _ => "Other / not yet categorized",
    };

    private static bool ContainsAny(string name, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (name.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NameIn(string name, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.Equals(name, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
