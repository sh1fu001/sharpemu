# Kernel HLE Status

_223 `libKernel` exports. 85 triaged, 138 not yet triaged._ Areas are inferred from export names; severities are a curated triage seed. Regenerate with `SharpEmu --kernel-status`.

Severity: **BLOCKER** prevents boot; **CRITICAL** crash/deadlock; **VISIBLE** graphics/audio/input; **COSMETIC** minor; **UNKNOWN** not yet triaged.

## Summary by area

| # | Area | Exports | BLOCKER | CRITICAL | VISIBLE | COSMETIC | UNKNOWN |
| - | ---- | ------: | ------: | -------: | ------: | -------: | ------: |
| 1 | Thread lifecycle (pthread) | 52 | 4 | 10 | 0 | 6 | 32 |
| 2 | Synchronization (mutex / condvar / semaphore) | 53 | 2 | 21 | 0 | 0 | 30 |
| 3 | Event queue & events | 23 | 0 | 7 | 2 | 1 | 13 |
| 4 | Memory mapping | 20 | 6 | 2 | 2 | 0 | 10 |
| 5 | Module loading | 10 | 1 | 0 | 0 | 3 | 6 |
| 6 | File descriptors | 21 | 0 | 0 | 4 | 4 | 13 |
| 7 | Time / clock / sleep | 16 | 0 | 0 | 7 | 0 | 9 |
| 8 | Process params | 8 | 1 | 0 | 0 | 2 | 5 |
| - | Other / not yet categorized | 20 | 0 | 0 | 0 | 0 | 20 |

## 1. Thread lifecycle (pthread) (52)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| __tls_get_addr | `vNe1w4diLCs` | BLOCKER | Thread-local storage base, touched extremely early. |
| pthread_create | `OxhIB8LB-PQ` | BLOCKER | POSIX thread creation used by the C++ runtime. |
| pthread_create_name_np | `Jmi+9w9u0E4` | BLOCKER | Named thread creation used during init. |
| scePthreadCreate | `6UgtwV+0zb4` | BLOCKER | Guest worker threads never start without it. |
| pthread_join | `h9CcP3J0oVM` | CRITICAL | A wrong join blocks the joining thread forever. |
| pthread_once | `__hle_pthread_once` | CRITICAL | One-time init guard; double-run corrupts state. |
| pthread_self | `EotR8a3ASf4` | CRITICAL | Identity used by locks and TLS. |
| scePthreadExit | `3kg7rT0NQIs` | CRITICAL | Thread teardown; leaks or hangs if mismodelled. |
| scePthreadGetspecific | `eoht7mQOCmo` | CRITICAL | TLS slot read. |
| scePthreadJoin | `onNY9Byn-W8` | CRITICAL | A wrong join blocks the joining thread forever. |
| scePthreadKeyCreate | `geDaqgH9lTg` | CRITICAL | TLS key allocation. |
| scePthreadOnce | `14bOACANTBo` | CRITICAL | One-time init guard; double-run corrupts state. |
| scePthreadSelf | `aI+OeCz8xrQ` | CRITICAL | Identity used by locks and TLS. |
| scePthreadSetspecific | `+BzXYkqYeLE` | CRITICAL | TLS slot write. |
| scePthreadGetaffinity | `rcrVFJsQWRY` | COSMETIC | Core affinity hint; safe to approximate. |
| scePthreadGetname | `How7B8Oet6k` | COSMETIC | Debug thread name. |
| scePthreadGetprio | `1tKyG7RlMJo` | COSMETIC | Scheduling hint; safe to approximate. |
| scePthreadSetaffinity | `bt3CTBKmGyI` | COSMETIC | Core affinity hint; safe to ignore. |
| scePthreadSetprio | `W0Hpm2X0uPE` | COSMETIC | Scheduling hint; safe to approximate. |
| scePthreadYield | `T72hz6ffq08` | COSMETIC | Scheduling hint. |
| __pthread_cxa_finalize | `kbw4UHHSYy0` | UNKNOWN | Not yet triaged. |
| _sceKernelRtldThreadAtexitDecrement | `8OnWXlgQlvo` | UNKNOWN | Not yet triaged. |
| _sceKernelRtldThreadAtexitIncrement | `Tz4RNUCBbGI` | UNKNOWN | Not yet triaged. |
| _sceKernelSetThreadAtexitCount | `pB-yGZ2nQ9o` | UNKNOWN | Not yet triaged. |
| _sceKernelSetThreadAtexitReport | `WhCc1w3EhSI` | UNKNOWN | Not yet triaged. |
| _sceKernelSetThreadDtors | `rNhWz+lvOMU` | UNKNOWN | Not yet triaged. |
| pthread_attr_destroy | `zHchY8ft5pk` | UNKNOWN | Not yet triaged. |
| pthread_attr_init | `wtkt-teR1so` | UNKNOWN | Not yet triaged. |
| pthread_attr_setdetachstate | `__hle_attr_detach` | UNKNOWN | Not yet triaged. |
| pthread_attr_setstacksize | `2Q0z6rnBrTE` | UNKNOWN | Not yet triaged. |
| pthread_getspecific | `0-KXaS70xy4` | UNKNOWN | Not yet triaged. |
| pthread_getthreadid_np | `3eqs37G74-s` | UNKNOWN | Not yet triaged. |
| pthread_key_create | `mqULNdimTn0` | UNKNOWN | Not yet triaged. |
| pthread_key_delete | `6BpEZuDT7YI` | UNKNOWN | Not yet triaged. |
| pthread_setspecific | `WrOLvHU0yQM` | UNKNOWN | Not yet triaged. |
| scePthreadAttrDestroy | `62KCwEMmzcM` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGet | `x1X76arYMxU` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetaffinity | `8+s5BzZjxSg` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetdetachstate | `JaRMy+QcpeU` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetguardsize | `txHtngJ+eyc` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetstack | `-quPa4SEJUw` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetstackaddr | `Ru36fiTtJzA` | UNKNOWN | Not yet triaged. |
| scePthreadAttrGetstacksize | `-fA+7ZlGDQs` | UNKNOWN | Not yet triaged. |
| scePthreadAttrInit | `nsYoNRywwNg` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetaffinity | `3qxgM4ezETA` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetdetachstate | `-Wreprtu0Qs` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetguardsize | `El+cQ20DynU` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetstacksize | `UTXzJbWhhTE` | UNKNOWN | Not yet triaged. |
| scePthreadDetach | `4qGrR6eoP9Y` | UNKNOWN | Not yet triaged. |
| scePthreadEqual | `3PtV6p3QNX4` | UNKNOWN | Not yet triaged. |
| scePthreadGetthreadid | `EI-5-jlq2dE` | UNKNOWN | Not yet triaged. |
| scePthreadKeyDelete | `PrdHuuDekhY` | UNKNOWN | Not yet triaged. |

## 2. Synchronization (mutex / condvar / semaphore) (53)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| pthread_mutex_init | `ttHNfU+qDBU` | BLOCKER | Mutexes are created pervasively during init. |
| scePthreadMutexInit | `cmo1RIYva9o` | BLOCKER | Mutexes are created pervasively during init. |
| pthread_cond_broadcast | `mkx2fVhNMsg` | CRITICAL | A dropped broadcast deadlocks waiters. |
| pthread_cond_signal | `2MOy+rUfuhQ` | CRITICAL | A dropped signal deadlocks the waiter. |
| pthread_cond_wait | `Op8TBGY5KHg` | CRITICAL | Missed wake-ups deadlock the waiter. |
| pthread_mutex_lock | `7H0iTOciTLo` | CRITICAL | Incorrect locking deadlocks the guest. |
| pthread_mutex_trylock | `K-jXhbt2gn4` | CRITICAL | Wrong result spins or deadlocks. |
| pthread_mutex_unlock | `2Z+PpY6CaJg` | CRITICAL | Missing unlock deadlocks waiters. |
| sceKernelCreateSema | `188x57JYp0g` | CRITICAL | Semaphore object creation. |
| sceKernelPollSema | `12wOHk8ywb0` | CRITICAL | Non-blocking acquire; wrong result mis-sequences. |
| sceKernelSignalSema | `4czppHBiriw` | CRITICAL | Missing signal deadlocks waiters. |
| sceKernelWaitSema | `Zxa0VhQVTsk` | CRITICAL | Blocking wait; wrong count deadlocks. |
| scePthreadCondBroadcast | `JGgj7Uvrl+A` | CRITICAL | A dropped broadcast deadlocks waiters. |
| scePthreadCondInit | `2Tb92quprl0` | CRITICAL | Condition variable setup. |
| scePthreadCondSignal | `kDh-NfxgMtE` | CRITICAL | A dropped signal deadlocks the waiter. |
| scePthreadCondTimedwait | `BmMjYxmew1w` | CRITICAL | Missed wake-ups / bad timeout deadlock or busy-loop. |
| scePthreadCondWait | `WKAXJ4XBPQ4` | CRITICAL | Missed wake-ups deadlock the waiter. |
| scePthreadMutexLock | `9UK1vLZQft4` | CRITICAL | Incorrect locking deadlocks the guest. |
| scePthreadMutexTrylock | `upoVrzMHFeE` | CRITICAL | Wrong result spins or deadlocks. |
| scePthreadMutexUnlock | `tn3VlD0hG60` | CRITICAL | Missing unlock deadlocks waiters. |
| sem_init | `__hle_sem_init` | CRITICAL | POSIX semaphore creation. |
| sem_post | `__hle_sem_post` | CRITICAL | Missing post deadlocks waiters. |
| sem_wait | `__hle_sem_wait` | CRITICAL | Blocking wait; wrong count deadlocks. |
| pthread_cond_init | `0TyVk4MSLt0` | UNKNOWN | Not yet triaged. |
| pthread_mutex_destroy | `ltCfaGr2JGE` | UNKNOWN | Not yet triaged. |
| pthread_mutexattr_destroy | `HF7lK46xzjY` | UNKNOWN | Not yet triaged. |
| pthread_mutexattr_init | `dQHWEsJtoE4` | UNKNOWN | Not yet triaged. |
| pthread_mutexattr_setprotocol | `5txKfcMUAok` | UNKNOWN | Not yet triaged. |
| pthread_mutexattr_settype | `mDmgMOGVUqg` | UNKNOWN | Not yet triaged. |
| pthread_rwlock_destroy | `1471ajPzxh0` | UNKNOWN | Not yet triaged. |
| pthread_rwlock_init | `ytQULN-nhL4` | UNKNOWN | Not yet triaged. |
| pthread_rwlock_rdlock | `iGjsr1WAtI0` | UNKNOWN | Not yet triaged. |
| pthread_rwlock_unlock | `EgmLo6EWgso` | UNKNOWN | Not yet triaged. |
| pthread_rwlock_wrlock | `sIlRvQqsN2Y` | UNKNOWN | Not yet triaged. |
| sceKernelCancelSema | `4DM06U2BNEY` | UNKNOWN | Not yet triaged. |
| sceKernelDeleteSema | `R1Jvn8bSCW8` | UNKNOWN | Not yet triaged. |
| scePthreadCondDestroy | `g+PZd2hiacg` | UNKNOWN | Not yet triaged. |
| scePthreadCondattrDestroy | `waPcxYiR3WA` | UNKNOWN | Not yet triaged. |
| scePthreadCondattrInit | `m5-2bsNfv7s` | UNKNOWN | Not yet triaged. |
| scePthreadMutexDestroy | `2Of0f+3mhhE` | UNKNOWN | Not yet triaged. |
| scePthreadMutexattrDestroy | `smWEktiyyG0` | UNKNOWN | Not yet triaged. |
| scePthreadMutexattrInit | `F8bUHwAG284` | UNKNOWN | Not yet triaged. |
| scePthreadMutexattrSetprotocol | `1FGvU0i9saQ` | UNKNOWN | Not yet triaged. |
| scePthreadMutexattrSettype | `iMp8QpE+XO4` | UNKNOWN | Not yet triaged. |
| scePthreadRwlockDestroy | `BB+kb08Tl9A` | UNKNOWN | Not yet triaged. |
| scePthreadRwlockInit | `6ULAa0fq4jA` | UNKNOWN | Not yet triaged. |
| scePthreadRwlockRdlock | `Ox9i0c7L5w0` | UNKNOWN | Not yet triaged. |
| scePthreadRwlockUnlock | `+L98PIbGttk` | UNKNOWN | Not yet triaged. |
| scePthreadRwlockWrlock | `mqdNorrB+gI` | UNKNOWN | Not yet triaged. |
| sem_destroy | `__hle_sem_destroy` | UNKNOWN | Not yet triaged. |
| sem_getvalue | `__hle_sem_getvalue` | UNKNOWN | Not yet triaged. |
| sem_timedwait | `__hle_sem_timedwait` | UNKNOWN | Not yet triaged. |
| sem_trywait | `__hle_sem_trywait` | UNKNOWN | Not yet triaged. |

## 3. Event queue & events (23)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| sceKernelCreateEqueue | `D0OdFMjp46I` | CRITICAL | Event queue used for flips, timers and I/O. |
| sceKernelCreateEventFlag | `BpFoboUJoZU` | CRITICAL | Event-flag object creation. |
| sceKernelGetEventData | `kwGyyjohI50` | CRITICAL | Event payload read after a wait. |
| sceKernelGetEventId | `mJ7aghmgvfc` | CRITICAL | Event identity read after a wait. |
| sceKernelSetEventFlag | `IOnSvHzqu6A` | CRITICAL | Missing set deadlocks waiters. |
| sceKernelWaitEqueue | `fzyMKs9kim0` | CRITICAL | Blocking wait; dropped events deadlock. |
| sceKernelWaitEventFlag | `JTvBflhYazQ` | CRITICAL | Blocking wait; wrong bits deadlock. |
| sceKernelAddUserEvent | `4R6-OvI2cEA` | VISIBLE | User events drive frame/timer callbacks. |
| sceKernelTriggerUserEvent | `F6e0kwo4cnk` | VISIBLE | Missing triggers stall dependent work. |
| sceKernelDeleteEqueue | `jpFjmgAC5AE` | COSMETIC | Teardown. |
| sceKernelAddAmprEvent | `bBfz7kMF2Ho` | UNKNOWN | Not yet triaged. |
| sceKernelAddAmprSystemEvent | `vuae5JPNt9A` | UNKNOWN | Not yet triaged. |
| sceKernelAddUserEventEdge | `WDszmSbWuDk` | UNKNOWN | Not yet triaged. |
| sceKernelCancelEventFlag | `PZku4ZrXJqg` | UNKNOWN | Not yet triaged. |
| sceKernelClearEventFlag | `7uhBFWRAS60` | UNKNOWN | Not yet triaged. |
| sceKernelDeleteAmprEvent | `bMmid3pfyjo` | UNKNOWN | Not yet triaged. |
| sceKernelDeleteAmprSystemEvent | `Ij+ryuEClXQ` | UNKNOWN | Not yet triaged. |
| sceKernelDeleteEventFlag | `8mql9OcQnd4` | UNKNOWN | Not yet triaged. |
| sceKernelDeleteUserEvent | `LJDwdSNTnDg` | UNKNOWN | Not yet triaged. |
| sceKernelGetEventFilter | `23CPPI1tyBY` | UNKNOWN | Not yet triaged. |
| sceKernelGetEventUserData | `vz+pg2zdopI` | UNKNOWN | Not yet triaged. |
| sceKernelGetKqueueFromEqueue | `QyrxcdBrb0M` | UNKNOWN | Not yet triaged. |
| sceKernelPollEventFlag | `9lvj5DjHZiA` | UNKNOWN | Not yet triaged. |

## 4. Memory mapping (20)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| sceKernelAllocateDirectMemory | `rTXw65xmLIA` | BLOCKER | Physical memory pool the title maps from. |
| sceKernelAllocateMainDirectMemory | `B+vc2AO2Zrc` | BLOCKER | Main direct-memory pool allocation. |
| sceKernelMapDirectMemory | `L-Q3LEjIbgA` | BLOCKER | Maps direct memory into the address space. |
| sceKernelMapFlexibleMemory | `IWIBBdTHit4` | BLOCKER | Flexible (CPU) memory used for the guest heap. |
| sceKernelMapNamedDirectMemory | `NcaWUxfMNIQ` | BLOCKER | Maps named direct memory into the address space. |
| sceKernelMapNamedFlexibleMemory | `mL8NDH86iQI` | BLOCKER | Named flexible memory used for the guest heap. |
| sceKernelMprotect | `vSMAm3cxYTY` | CRITICAL | Protection change; wrong perms fault later. |
| sceKernelMunmap | `cQke9UuBQOk` | CRITICAL | Unmap; a wrong unmap frees live memory. |
| sceKernelQueryMemoryProtection | `WFcfL2lzido` | VISIBLE | Introspection used by allocators. |
| sceKernelVirtualQuery | `rVjRvHJ0X6c` | VISIBLE | Introspection used by allocators. |
| sceKernelAvailableDirectMemorySize | `C0f7TJcbfac` | UNKNOWN | Not yet triaged. |
| sceKernelAvailableFlexibleMemorySize | `aNz11fnnzi4` | UNKNOWN | Not yet triaged. |
| sceKernelBatchMap | `2SKEx6bSq-4` | UNKNOWN | Not yet triaged. |
| sceKernelBatchMap2 | `kBJzF8x4SyE` | UNKNOWN | Not yet triaged. |
| sceKernelDirectMemoryQuery | `BHouLQzh0X0` | UNKNOWN | Not yet triaged. |
| sceKernelGetDirectMemorySize | `pO96TwzOm5E` | UNKNOWN | Not yet triaged. |
| sceKernelMtypeprotect | `9bfdLIyuwCY` | UNKNOWN | Not yet triaged. |
| sceKernelReleaseDirectMemory | `MBuItvba6z8` | UNKNOWN | Not yet triaged. |
| sceKernelReserveVirtualRange | `7oxv3PPCumo` | UNKNOWN | Not yet triaged. |
| sceKernelSetPrtAperture | `BohYr-F7-is` | UNKNOWN | Not yet triaged. |

## 5. Module loading (10)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| sceKernelLoadStartModule | `wzvqT4UqKX8` | BLOCKER | Loads and starts the .prx modules the title needs. |
| sceKernelGetModuleInfo | `kUpgrXIrz7Q` | COSMETIC | Introspection; often only used for logging/unwind. |
| sceKernelGetModuleList | `IuxnUuXk6Bg` | COSMETIC | Introspection; often only used for logging/unwind. |
| sceKernelStopUnloadModule | `QKd0qM58Qes` | COSMETIC | Module teardown. |
| __elf_phdr_match_addr | `Fjc4-n1+y2g` | UNKNOWN | Not yet triaged. |
| sceKernelGetModuleInfo2 | `QgsKEUfkqMA` | UNKNOWN | Not yet triaged. |
| sceKernelGetModuleInfoForUnwind | `RpQJJVKTiFM` | UNKNOWN | Not yet triaged. |
| sceKernelGetModuleInfoFromAddr | `f7KBOafysXo` | UNKNOWN | Not yet triaged. |
| sceKernelGetModuleInfoInternal | `HZO7xOos4xc` | UNKNOWN | Not yet triaged. |
| sceKernelGetModuleList2 | `ZzzC3ZGVAkc` | UNKNOWN | Not yet triaged. |

## 6. File descriptors (21)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| open | `wuCroIGjt2g` | VISIBLE | File open; missing assets degrade content. |
| read | `AqBioC2vF3I` | VISIBLE | File read. |
| sceKernelOpen | `1G3lF1Gg1k8` | VISIBLE | File open; missing assets degrade content. |
| sceKernelRead | `Cg4srZ6TKbU` | VISIBLE | File read. |
| close | `bY-PO6JhzhQ` | COSMETIC | Descriptor teardown. |
| sceKernelClose | `UK2Tl2DWUns` | COSMETIC | Descriptor teardown. |
| sceKernelWrite | `4wSze92BhLI` | COSMETIC | File write; often logs/saves. |
| write | `FN4gaPmuFV8` | COSMETIC | File write; often logs/saves. |
| _close | `NNtFaKJbPt0` | UNKNOWN | Not yet triaged. |
| _open | `6c3rCVE-fTU` | UNKNOWN | Not yet triaged. |
| _read | `DRuBt2pvICk` | UNKNOWN | Not yet triaged. |
| _write | `FxVZqBAA7ks` | UNKNOWN | Not yet triaged. |
| lseek | `Oy6IpwgtYOk` | UNKNOWN | Not yet triaged. |
| sceKernelFstat | `kBwCPsYX-m4` | UNKNOWN | Not yet triaged. |
| sceKernelGetdents | `j2AIqSqJP0w` | UNKNOWN | Not yet triaged. |
| sceKernelGetdirentries | `taRWhTJFTgE` | UNKNOWN | Not yet triaged. |
| sceKernelLseek | `oib76F-12fk` | UNKNOWN | Not yet triaged. |
| sceKernelMkdir | `1-LFLmRFxxM` | UNKNOWN | Not yet triaged. |
| sceKernelRmdir | `naInUjYt3so` | UNKNOWN | Not yet triaged. |
| sceKernelStat | `eV9wAD2riIA` | UNKNOWN | Not yet triaged. |
| sceKernelUnlink | `AUXVxWeJU-A` | UNKNOWN | Not yet triaged. |

## 7. Time / clock / sleep (16)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| clock_gettime | `lLMT9vJAck0` | VISIBLE | Monotonic/real clock; drives animation timing. |
| gettimeofday | `n88vx3C5nW8` | VISIBLE | Wall clock; drives timing. |
| sceKernelClockGettime | `QBi7HCK03hw` | VISIBLE | Monotonic/real clock; drives animation timing. |
| sceKernelGetProcessTimeCounter | `fgxnMeTNUtY` | VISIBLE | High-resolution counter; pacing. |
| sceKernelGettimeofday | `ejekcaNQNq0` | VISIBLE | Wall clock; drives timing. |
| sceKernelReadTsc | `-2IRUCO--PM` | VISIBLE | Cycle counter; pacing. |
| sceKernelUsleep | `1jfXLRVzisc` | VISIBLE | Sleep pacing; wrong values stutter or busy-loop. |
| pthread_setschedparam | `Xs9hdiD7sAA` | UNKNOWN | Not yet triaged. |
| sceKernelConvertLocaltimeToUtc | `0NTHN1NKONI` | UNKNOWN | Not yet triaged. |
| sceKernelConvertUtcToLocaltime | `-o5uEDpN+oY` | UNKNOWN | Not yet triaged. |
| sceKernelGetProcessTime | `4J2sUJmuHZQ` | UNKNOWN | Not yet triaged. |
| sceKernelGetProcessTimeCounterFrequency | `BNowx2l588E` | UNKNOWN | Not yet triaged. |
| sceKernelGetTscFrequency | `1j3S3n-tTW4` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetinheritsched | `eXbUSpEaTsA` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetschedparam | `DzES9hQF4f4` | UNKNOWN | Not yet triaged. |
| scePthreadAttrSetschedpolicy | `4+h9EzwKF4I` | UNKNOWN | Not yet triaged. |

## 8. Process params (8)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| sceKernelGetProcParam | `959qrazPIrg` | BLOCKER | Process parameters read during early runtime init. |
| sceKernelGetCompiledSdkVersion | `WB66evu8bsU` | COSMETIC | SDK version gate; usually just compared. |
| sceKernelIsNeoMode | `WslcK1FQcGI` | COSMETIC | PS4 Pro / Neo mode query. |
| _sceKernelRtldSetApplicationHeapAPI | `p5EcQeEeJAE` | UNKNOWN | Not yet triaged. |
| getargc | `__hle_getargc` | UNKNOWN | Not yet triaged. |
| getargv | `__hle_getargv` | UNKNOWN | Not yet triaged. |
| sceKernelGetGPI | `4oXYe9Xmk0Q` | UNKNOWN | Not yet triaged. |
| sceKernelSetGPO | `ca7v6Cxulzs` | UNKNOWN | Not yet triaged. |

## Other / not yet categorized (20)

| Export | NID | Severity | Note |
| ------ | --- | -------- | ---- |
| __error | `9BcDykPmo1I` | UNKNOWN | Not yet triaged. |
| __stack_chk_fail | `Ou3iL1abvng` | UNKNOWN | Not yet triaged. |
| __stack_chk_guard | `f7uOxY9mM1U` | UNKNOWN | Not yet triaged. |
| _exit | `6Z83sYWFlA8` | UNKNOWN | Not yet triaged. |
| _sigprocmask | `6xVpy0Fdq+I` | UNKNOWN | Not yet triaged. |
| exit | `uMei1W9uyNo` | UNKNOWN | Not yet triaged. |
| sceKernelAioInitializeImpl | `vYU8P9Td2Zo` | UNKNOWN | Not yet triaged. |
| sceKernelAioInitializeParam | `nu4a0-arQis` | UNKNOWN | Not yet triaged. |
| sceKernelAprResolveFilepathsToIdsAndFileSizes | `gEpBkcwxUjw` | UNKNOWN | Not yet triaged. |
| sceKernelAprSubmitCommandBuffer | `eE4Szl8sil8` | UNKNOWN | Not yet triaged. |
| sceKernelAprSubmitCommandBufferAndGetId | `qvMUCyyaCSI` | UNKNOWN | Not yet triaged. |
| sceKernelAprSubmitCommandBufferAndGetResult | `ASoW5WE-UPo` | UNKNOWN | Not yet triaged. |
| sceKernelAprWaitCommandBuffer | `rqwFKI4PAiM` | UNKNOWN | Not yet triaged. |
| sceKernelDebugRaiseException | `OMDRKKAZ8I4` | UNKNOWN | Not yet triaged. |
| sceKernelDebugRaiseExceptionOnReleaseMode | `zE-wXIZjLoM` | UNKNOWN | Not yet triaged. |
| sceKernelExitSblock | `Ac86z8q7T8A` | UNKNOWN | Not yet triaged. |
| sceKernelGetSanitizerMallocReplaceExternal | `py6L8jiVAN8` | UNKNOWN | Not yet triaged. |
| sceKernelGetSanitizerNewReplaceExternal | `bnZxYgAFeA0` | UNKNOWN | Not yet triaged. |
| sceKernelIsAddressSanitizerEnabled | `jh+8XiK4LeE` | UNKNOWN | Not yet triaged. |
| sceKernelUuidCreate | `Xjoosiw+XPI` | UNKNOWN | Not yet triaged. |
