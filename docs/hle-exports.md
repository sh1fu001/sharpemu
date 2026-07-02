# HLE Export Coverage

This document summarizes the current High-Level Emulation export coverage.
It is generated from the `[SysAbiExport]` registrations used by the runtime.

Registration means that SharpEmu can resolve an export by module and NID. It
does not, by itself, prove that the export is fully implemented or accurate.

## Summary

- Total modules: 38
- Total exports: 540 registered exports
- Implemented: Not available in the generated JSON report
- Stubbed: Not available in the generated JSON report
- Missing: Not available in the generated JSON report
- Unknown: 540 registered exports are not classified by implementation depth

## Module Overview

| Module | Implemented | Stubbed | Missing | Unknown | Total |
|---|---:|---:|---:|---:|---:|
| libc | — | — | — | 88 | 88 |
| LibcInternalExt | — | — | — | 1 | 1 |
| libKernel | — | — | — | 223 | 223 |
| libSceAgc | — | — | — | 51 | 51 |
| libSceAgcDriver | — | — | — | 4 | 4 |
| libSceAmpr | — | — | — | 15 | 15 |
| libSceAppContent | — | — | — | 4 | 4 |
| libSceAudioOut | — | — | — | 4 | 4 |
| libSceAudioOut2 | — | — | — | 11 | 11 |
| libSceCommonDialog | — | — | — | 2 | 2 |
| libSceCoredump | — | — | — | 1 | 1 |
| libSceFiber | — | — | — | 15 | 15 |
| libSceGameUpdate | — | — | — | 1 | 1 |
| libSceHttp | — | — | — | 4 | 4 |
| libSceHttp2 | — | — | — | 2 | 2 |
| libSceJson | — | — | — | 5 | 5 |
| libSceKeyboard | — | — | — | 4 | 4 |
| libSceNet | — | — | — | 7 | 7 |
| libSceNetCtl | — | — | — | 6 | 6 |
| libSceNpEntitlementAccess | — | — | — | 1 | 1 |
| libSceNpGameIntent | — | — | — | 1 | 1 |
| libSceNpManager | — | — | — | 6 | 6 |
| libSceNpManagerForToolkit | — | — | — | 1 | 1 |
| libSceNpSessionSignaling | — | — | — | 1 | 1 |
| libSceNpUniversalDataSystem | — | — | — | 4 | 4 |
| libSceNpWebApi2 | — | — | — | 2 | 2 |
| libScePad | — | — | — | 10 | 10 |
| libScePlayGo | — | — | — | 15 | 15 |
| libScePosix | — | — | — | 1 | 1 |
| libSceRtc | — | — | — | 4 | 4 |
| libSceSaveData | — | — | — | 2 | 2 |
| libSceShareUtility | — | — | — | 2 | 2 |
| libSceSsl | — | — | — | 3 | 3 |
| libSceSysmodule | — | — | — | 4 | 4 |
| libSceSystemGesture | — | — | — | 6 | 6 |
| libSceSystemService | — | — | — | 4 | 4 |
| libSceUserService | — | — | — | 7 | 7 |
| libSceVideoOut | — | — | — | 18 | 18 |

Detailed per-export registration data is listed below. Detailed per-export
implementation, stub, and missing status is not currently available in the
generated JSON report. The `Target` column records the guest generation for
which each export is registered.

Regenerate the machine-readable report with:

```powershell
SharpEmu --export-report
```

## libc (88)

| Export | NID | Target |
| --- | --- | --- |
| __cxa_atexit | `tsvEmnenz48` | Gen4, Gen5 |
| __cxa_finalize | `H2e8t5ScQGc` | Gen4, Gen5 |
| __cxa_guard_abort | `2emaaluWzUw` | Gen4, Gen5 |
| __cxa_guard_acquire | `3GPpjQdAMTw` | Gen4, Gen5 |
| __cxa_guard_release | `9rAeANT2tyE` | Gen4, Gen5 |
| _init_env | `bzQExy189ZI` | Gen4, Gen5 |
| aligned_alloc | `2Btkg8k24Zg` | Gen4, Gen5 |
| atexit | `8G2LB+A3rzg` | Gen4, Gen5 |
| atoi | `__hle_atoi` | Gen4, Gen5 |
| calloc | `2X5agFjKxMc` | Gen4, Gen5 |
| catchReturnFromMain | `XKRegsFpEpk` | Gen4, Gen5 |
| closedir | `__hle_closedir` | Gen4, Gen5 |
| fclose | `__hle_fclose` | Gen4, Gen5 |
| fdopen | `__hle_fdopen` | Gen4, Gen5 |
| ferror | `__hle_ferror` | Gen4, Gen5 |
| fflush | `__hle_fflush` | Gen4, Gen5 |
| fgetc | `__hle_fgetc` | Gen4, Gen5 |
| fgets | `__hle_fgets` | Gen4, Gen5 |
| fileno | `__hle_fileno` | Gen4, Gen5 |
| fopen | `__hle_fopen` | Gen4, Gen5 |
| fprintf | `__hle_fprintf` | Gen4, Gen5 |
| fputc | `__hle_fputc` | Gen4, Gen5 |
| fputs | `QrZZdJ8XsX0` | Gen4, Gen5 |
| fread | `__hle_fread` | Gen4, Gen5 |
| free | `tIhsqj0qsFE` | Gen4, Gen5 |
| fseek | `__hle_fseek` | Gen4, Gen5 |
| fseeko | `__hle_fseeko` | Gen4, Gen5 |
| fstat | `__hle_fstat` | Gen4, Gen5 |
| ftell | `__hle_ftell` | Gen4, Gen5 |
| ftello | `__hle_ftello` | Gen4, Gen5 |
| fwrite | `__hle_fwrite` | Gen4, Gen5 |
| getenv | `__hle_getenv` | Gen4, Gen5 |
| malloc | `gQX+4GDQjpM` | Gen4, Gen5 |
| memalign | `Ujf3KzMvRmI` | Gen4, Gen5 |
| memchr | `__hle_memchr` | Gen4, Gen5 |
| memcmp | `DfivPArhucg` | Gen4, Gen5 |
| memcpy | `Q3VBxCXhUHs` | Gen4, Gen5 |
| memmove | `+P6FRGH4LfA` | Gen4, Gen5 |
| memset | `8zTFvBIAIN8` | Gen4, Gen5 |
| mkdir | `__hle_mkdir` | Gen4, Gen5 |
| opendir | `__hle_opendir` | Gen4, Gen5 |
| perror | `EMutwaQ34Jo` | Gen4, Gen5 |
| posix_memalign | `cVSk9y8URbc` | Gen4, Gen5 |
| printf | `hcuQgD53UxM` | Gen4, Gen5 |
| puts | `__hle_puts` | Gen4, Gen5 |
| rand | `__hle_rand` | Gen4, Gen5 |
| readdir | `__hle_readdir` | Gen4, Gen5 |
| realloc | `Y7aJ1uydPMo` | Gen4, Gen5 |
| remove | `__hle_remove` | Gen4, Gen5 |
| setjmp | `__hle_setjmp` | Gen4, Gen5 |
| snprintf | `eLdDw6l0-bU` | Gen4, Gen5 |
| srand | `__hle_srand` | Gen4, Gen5 |
| stat | `__hle_stat` | Gen4, Gen5 |
| strchr | `__hle_strchr` | Gen4, Gen5 |
| strcmp | `Ovb2dSJOAuE` | Gen4, Gen5 |
| strcpy | `kiZSXIWd9vg` | Gen4, Gen5 |
| strlcat | `__hle_strlcat` | Gen4, Gen5 |
| strlcpy | `__hle_strlcpy` | Gen4, Gen5 |
| strlen | `j4ViWNHEgww` | Gen4, Gen5 |
| strncmp | `aesyjrHVWy4` | Gen4, Gen5 |
| strncpy | `6sJWiWSRuqk` | Gen4, Gen5 |
| strnlen | `5jNubw4vlAA` | Gen4, Gen5 |
| strrchr | `__hle_strrchr` | Gen4, Gen5 |
| strstr | `__hle_strstr` | Gen4, Gen5 |
| strtol | `__hle_strtol` | Gen4, Gen5 |
| strtoll | `__hle_strtoll` | Gen4, Gen5 |
| strtoul | `__hle_strtoul` | Gen4, Gen5 |
| strtoull | `__hle_strtoull` | Gen4, Gen5 |
| swprintf | `nJz16JE1txM` | Gen4, Gen5 |
| swprintf_s | `Im55VJ-Bekc` | Gen4, Gen5 |
| time | `__hle_time` | Gen4, Gen5 |
| tolower | `__hle_tolower` | Gen4, Gen5 |
| toupper | `__hle_toupper` | Gen4, Gen5 |
| ungetc | `__hle_ungetc` | Gen4, Gen5 |
| vprintf | `GMpvxPFW924` | Gen4, Gen5 |
| vsnprintf | `Q2V+iqvjgC0` | Gen4, Gen5 |
| vswprintf | `u0XOsuOmOzc` | Gen4, Gen5 |
| vswprintf_s | `oDoV9tyHTbA` | Gen4, Gen5 |
| wcschr | `Ezzq78ZgHPs` | Gen4, Gen5 |
| wcscmp | `pNtJdE3x49E` | Gen4, Gen5 |
| wcscoll | `fV2xHER+bKE` | Gen4, Gen5 |
| wcscpy | `FM5NPnLqBc8` | Gen4, Gen5 |
| wcscpy_s | `6f5f-qx4ucA` | Gen4, Gen5 |
| wcslen | `LHMrG7e8G78` | Gen4, Gen5 |
| wcslen | `WkkeywLJcgU` | Gen4, Gen5 |
| wcsncmp | `E8wCoUEbfzk` | Gen4, Gen5 |
| wcsncpy | `0nV21JjYCH8` | Gen4, Gen5 |
| wcsncpy_s | `Slmz4HMpNGs` | Gen4, Gen5 |

## LibcInternalExt (1)

| Export | NID | Target |
| --- | --- | --- |
| LibcHeapGetTraceInfo | `NWtTN10cJzE` | Gen4, Gen5 |

## libKernel (223)

| Export | NID | Target |
| --- | --- | --- |
| __elf_phdr_match_addr | `Fjc4-n1+y2g` | Gen4, Gen5 |
| __error | `9BcDykPmo1I` | Gen4, Gen5 |
| __pthread_cxa_finalize | `kbw4UHHSYy0` | Gen4, Gen5 |
| __stack_chk_fail | `Ou3iL1abvng` | Gen4, Gen5 |
| __stack_chk_guard | `f7uOxY9mM1U` | Gen4, Gen5 |
| __tls_get_addr | `vNe1w4diLCs` | Gen4, Gen5 |
| _close | `NNtFaKJbPt0` | Gen4, Gen5 |
| _exit | `6Z83sYWFlA8` | Gen4, Gen5 |
| _open | `6c3rCVE-fTU` | Gen4, Gen5 |
| _read | `DRuBt2pvICk` | Gen4, Gen5 |
| _sceKernelRtldSetApplicationHeapAPI | `p5EcQeEeJAE` | Gen4, Gen5 |
| _sceKernelRtldThreadAtexitDecrement | `8OnWXlgQlvo` | Gen4, Gen5 |
| _sceKernelRtldThreadAtexitIncrement | `Tz4RNUCBbGI` | Gen4, Gen5 |
| _sceKernelSetThreadAtexitCount | `pB-yGZ2nQ9o` | Gen4, Gen5 |
| _sceKernelSetThreadAtexitReport | `WhCc1w3EhSI` | Gen4, Gen5 |
| _sceKernelSetThreadDtors | `rNhWz+lvOMU` | Gen4, Gen5 |
| _sigprocmask | `6xVpy0Fdq+I` | Gen4, Gen5 |
| _write | `FxVZqBAA7ks` | Gen4, Gen5 |
| clock_gettime | `lLMT9vJAck0` | Gen4, Gen5 |
| close | `bY-PO6JhzhQ` | Gen4, Gen5 |
| exit | `uMei1W9uyNo` | Gen4, Gen5 |
| getargc | `__hle_getargc` | Gen4, Gen5 |
| getargv | `__hle_getargv` | Gen4, Gen5 |
| gettimeofday | `n88vx3C5nW8` | Gen4, Gen5 |
| lseek | `Oy6IpwgtYOk` | Gen4, Gen5 |
| open | `wuCroIGjt2g` | Gen4, Gen5 |
| pthread_attr_destroy | `zHchY8ft5pk` | Gen4, Gen5 |
| pthread_attr_init | `wtkt-teR1so` | Gen4, Gen5 |
| pthread_attr_setdetachstate | `__hle_attr_detach` | Gen4, Gen5 |
| pthread_attr_setstacksize | `2Q0z6rnBrTE` | Gen4, Gen5 |
| pthread_cond_broadcast | `mkx2fVhNMsg` | Gen4, Gen5 |
| pthread_cond_init | `0TyVk4MSLt0` | Gen4, Gen5 |
| pthread_cond_signal | `2MOy+rUfuhQ` | Gen4, Gen5 |
| pthread_cond_wait | `Op8TBGY5KHg` | Gen4, Gen5 |
| pthread_create | `OxhIB8LB-PQ` | Gen4, Gen5 |
| pthread_create_name_np | `Jmi+9w9u0E4` | Gen4, Gen5 |
| pthread_getspecific | `0-KXaS70xy4` | Gen4, Gen5 |
| pthread_getthreadid_np | `3eqs37G74-s` | Gen4, Gen5 |
| pthread_join | `h9CcP3J0oVM` | Gen4, Gen5 |
| pthread_key_create | `mqULNdimTn0` | Gen4, Gen5 |
| pthread_key_delete | `6BpEZuDT7YI` | Gen4, Gen5 |
| pthread_mutex_destroy | `ltCfaGr2JGE` | Gen4, Gen5 |
| pthread_mutex_init | `ttHNfU+qDBU` | Gen4, Gen5 |
| pthread_mutex_lock | `7H0iTOciTLo` | Gen4, Gen5 |
| pthread_mutex_trylock | `K-jXhbt2gn4` | Gen4, Gen5 |
| pthread_mutex_unlock | `2Z+PpY6CaJg` | Gen4, Gen5 |
| pthread_mutexattr_destroy | `HF7lK46xzjY` | Gen4, Gen5 |
| pthread_mutexattr_init | `dQHWEsJtoE4` | Gen4, Gen5 |
| pthread_mutexattr_setprotocol | `5txKfcMUAok` | Gen4, Gen5 |
| pthread_mutexattr_settype | `mDmgMOGVUqg` | Gen4, Gen5 |
| pthread_once | `__hle_pthread_once` | Gen4, Gen5 |
| pthread_rwlock_destroy | `1471ajPzxh0` | Gen4, Gen5 |
| pthread_rwlock_init | `ytQULN-nhL4` | Gen4, Gen5 |
| pthread_rwlock_rdlock | `iGjsr1WAtI0` | Gen4, Gen5 |
| pthread_rwlock_unlock | `EgmLo6EWgso` | Gen4, Gen5 |
| pthread_rwlock_wrlock | `sIlRvQqsN2Y` | Gen4, Gen5 |
| pthread_self | `EotR8a3ASf4` | Gen4, Gen5 |
| pthread_setschedparam | `Xs9hdiD7sAA` | Gen4, Gen5 |
| pthread_setspecific | `WrOLvHU0yQM` | Gen4, Gen5 |
| read | `AqBioC2vF3I` | Gen4, Gen5 |
| sceKernelAddAmprEvent | `bBfz7kMF2Ho` | Gen4, Gen5 |
| sceKernelAddAmprSystemEvent | `vuae5JPNt9A` | Gen4, Gen5 |
| sceKernelAddUserEvent | `4R6-OvI2cEA` | Gen4, Gen5 |
| sceKernelAddUserEventEdge | `WDszmSbWuDk` | Gen4, Gen5 |
| sceKernelAioInitializeImpl | `vYU8P9Td2Zo` | Gen4, Gen5 |
| sceKernelAioInitializeParam | `nu4a0-arQis` | Gen4, Gen5 |
| sceKernelAllocateDirectMemory | `rTXw65xmLIA` | Gen4, Gen5 |
| sceKernelAllocateMainDirectMemory | `B+vc2AO2Zrc` | Gen4, Gen5 |
| sceKernelAprResolveFilepathsToIdsAndFileSizes | `gEpBkcwxUjw` | Gen4, Gen5 |
| sceKernelAprSubmitCommandBuffer | `eE4Szl8sil8` | Gen4, Gen5 |
| sceKernelAprSubmitCommandBufferAndGetId | `qvMUCyyaCSI` | Gen4, Gen5 |
| sceKernelAprSubmitCommandBufferAndGetResult | `ASoW5WE-UPo` | Gen4, Gen5 |
| sceKernelAprWaitCommandBuffer | `rqwFKI4PAiM` | Gen4, Gen5 |
| sceKernelAvailableDirectMemorySize | `C0f7TJcbfac` | Gen4, Gen5 |
| sceKernelAvailableFlexibleMemorySize | `aNz11fnnzi4` | Gen4, Gen5 |
| sceKernelBatchMap | `2SKEx6bSq-4` | Gen4, Gen5 |
| sceKernelBatchMap2 | `kBJzF8x4SyE` | Gen4, Gen5 |
| sceKernelCancelEventFlag | `PZku4ZrXJqg` | Gen4, Gen5 |
| sceKernelCancelSema | `4DM06U2BNEY` | Gen4, Gen5 |
| sceKernelClearEventFlag | `7uhBFWRAS60` | Gen4, Gen5 |
| sceKernelClockGettime | `QBi7HCK03hw` | Gen4, Gen5 |
| sceKernelClose | `UK2Tl2DWUns` | Gen4, Gen5 |
| sceKernelConvertLocaltimeToUtc | `0NTHN1NKONI` | Gen4, Gen5 |
| sceKernelConvertUtcToLocaltime | `-o5uEDpN+oY` | Gen4, Gen5 |
| sceKernelCreateEqueue | `D0OdFMjp46I` | Gen4, Gen5 |
| sceKernelCreateEventFlag | `BpFoboUJoZU` | Gen4, Gen5 |
| sceKernelCreateSema | `188x57JYp0g` | Gen4, Gen5 |
| sceKernelDebugRaiseException | `OMDRKKAZ8I4` | Gen4, Gen5 |
| sceKernelDebugRaiseExceptionOnReleaseMode | `zE-wXIZjLoM` | Gen4, Gen5 |
| sceKernelDeleteAmprEvent | `bMmid3pfyjo` | Gen4, Gen5 |
| sceKernelDeleteAmprSystemEvent | `Ij+ryuEClXQ` | Gen4, Gen5 |
| sceKernelDeleteEqueue | `jpFjmgAC5AE` | Gen4, Gen5 |
| sceKernelDeleteEventFlag | `8mql9OcQnd4` | Gen4, Gen5 |
| sceKernelDeleteSema | `R1Jvn8bSCW8` | Gen4, Gen5 |
| sceKernelDeleteUserEvent | `LJDwdSNTnDg` | Gen4, Gen5 |
| sceKernelDirectMemoryQuery | `BHouLQzh0X0` | Gen4, Gen5 |
| sceKernelExitSblock | `Ac86z8q7T8A` | Gen4, Gen5 |
| sceKernelFstat | `kBwCPsYX-m4` | Gen4, Gen5 |
| sceKernelGetCompiledSdkVersion | `WB66evu8bsU` | Gen4, Gen5 |
| sceKernelGetDirectMemorySize | `pO96TwzOm5E` | Gen4, Gen5 |
| sceKernelGetEventData | `kwGyyjohI50` | Gen4, Gen5 |
| sceKernelGetEventFilter | `23CPPI1tyBY` | Gen4, Gen5 |
| sceKernelGetEventId | `mJ7aghmgvfc` | Gen4, Gen5 |
| sceKernelGetEventUserData | `vz+pg2zdopI` | Gen4, Gen5 |
| sceKernelGetGPI | `4oXYe9Xmk0Q` | Gen4, Gen5 |
| sceKernelGetKqueueFromEqueue | `QyrxcdBrb0M` | Gen4, Gen5 |
| sceKernelGetModuleInfo | `kUpgrXIrz7Q` | Gen4, Gen5 |
| sceKernelGetModuleInfo2 | `QgsKEUfkqMA` | Gen4, Gen5 |
| sceKernelGetModuleInfoForUnwind | `RpQJJVKTiFM` | Gen4, Gen5 |
| sceKernelGetModuleInfoFromAddr | `f7KBOafysXo` | Gen4, Gen5 |
| sceKernelGetModuleInfoInternal | `HZO7xOos4xc` | Gen4, Gen5 |
| sceKernelGetModuleList | `IuxnUuXk6Bg` | Gen4, Gen5 |
| sceKernelGetModuleList2 | `ZzzC3ZGVAkc` | Gen4, Gen5 |
| sceKernelGetProcParam | `959qrazPIrg` | Gen4, Gen5 |
| sceKernelGetProcessTime | `4J2sUJmuHZQ` | Gen4, Gen5 |
| sceKernelGetProcessTimeCounter | `fgxnMeTNUtY` | Gen4, Gen5 |
| sceKernelGetProcessTimeCounterFrequency | `BNowx2l588E` | Gen4, Gen5 |
| sceKernelGetSanitizerMallocReplaceExternal | `py6L8jiVAN8` | Gen4, Gen5 |
| sceKernelGetSanitizerNewReplaceExternal | `bnZxYgAFeA0` | Gen4, Gen5 |
| sceKernelGetTscFrequency | `1j3S3n-tTW4` | Gen4, Gen5 |
| sceKernelGetdents | `j2AIqSqJP0w` | Gen4, Gen5 |
| sceKernelGetdirentries | `taRWhTJFTgE` | Gen4, Gen5 |
| sceKernelGettimeofday | `ejekcaNQNq0` | Gen4, Gen5 |
| sceKernelIsAddressSanitizerEnabled | `jh+8XiK4LeE` | Gen4, Gen5 |
| sceKernelIsNeoMode | `WslcK1FQcGI` | Gen4, Gen5 |
| sceKernelLoadStartModule | `wzvqT4UqKX8` | Gen4, Gen5 |
| sceKernelLseek | `oib76F-12fk` | Gen4, Gen5 |
| sceKernelMapDirectMemory | `L-Q3LEjIbgA` | Gen4, Gen5 |
| sceKernelMapFlexibleMemory | `IWIBBdTHit4` | Gen4, Gen5 |
| sceKernelMapNamedDirectMemory | `NcaWUxfMNIQ` | Gen4, Gen5 |
| sceKernelMapNamedFlexibleMemory | `mL8NDH86iQI` | Gen4, Gen5 |
| sceKernelMkdir | `1-LFLmRFxxM` | Gen4, Gen5 |
| sceKernelMprotect | `vSMAm3cxYTY` | Gen4, Gen5 |
| sceKernelMtypeprotect | `9bfdLIyuwCY` | Gen4, Gen5 |
| sceKernelMunmap | `cQke9UuBQOk` | Gen4, Gen5 |
| sceKernelOpen | `1G3lF1Gg1k8` | Gen4, Gen5 |
| sceKernelPollEventFlag | `9lvj5DjHZiA` | Gen4, Gen5 |
| sceKernelPollSema | `12wOHk8ywb0` | Gen4, Gen5 |
| sceKernelQueryMemoryProtection | `WFcfL2lzido` | Gen4, Gen5 |
| sceKernelRead | `Cg4srZ6TKbU` | Gen4, Gen5 |
| sceKernelReadTsc | `-2IRUCO--PM` | Gen4, Gen5 |
| sceKernelReleaseDirectMemory | `MBuItvba6z8` | Gen4, Gen5 |
| sceKernelReserveVirtualRange | `7oxv3PPCumo` | Gen4, Gen5 |
| sceKernelRmdir | `naInUjYt3so` | Gen4, Gen5 |
| sceKernelSetEventFlag | `IOnSvHzqu6A` | Gen4, Gen5 |
| sceKernelSetGPO | `ca7v6Cxulzs` | Gen4, Gen5 |
| sceKernelSetPrtAperture | `BohYr-F7-is` | Gen4, Gen5 |
| sceKernelSignalSema | `4czppHBiriw` | Gen4, Gen5 |
| sceKernelStat | `eV9wAD2riIA` | Gen4, Gen5 |
| sceKernelStopUnloadModule | `QKd0qM58Qes` | Gen4, Gen5 |
| sceKernelTriggerUserEvent | `F6e0kwo4cnk` | Gen4, Gen5 |
| sceKernelUnlink | `AUXVxWeJU-A` | Gen4, Gen5 |
| sceKernelUsleep | `1jfXLRVzisc` | Gen4, Gen5 |
| sceKernelUuidCreate | `Xjoosiw+XPI` | Gen4, Gen5 |
| sceKernelVirtualQuery | `rVjRvHJ0X6c` | Gen4, Gen5 |
| sceKernelWaitEqueue | `fzyMKs9kim0` | Gen4, Gen5 |
| sceKernelWaitEventFlag | `JTvBflhYazQ` | Gen4, Gen5 |
| sceKernelWaitSema | `Zxa0VhQVTsk` | Gen4, Gen5 |
| sceKernelWrite | `4wSze92BhLI` | Gen4, Gen5 |
| scePthreadAttrDestroy | `62KCwEMmzcM` | Gen4, Gen5 |
| scePthreadAttrGet | `x1X76arYMxU` | Gen4, Gen5 |
| scePthreadAttrGetaffinity | `8+s5BzZjxSg` | Gen4, Gen5 |
| scePthreadAttrGetdetachstate | `JaRMy+QcpeU` | Gen4, Gen5 |
| scePthreadAttrGetguardsize | `txHtngJ+eyc` | Gen4, Gen5 |
| scePthreadAttrGetstack | `-quPa4SEJUw` | Gen4, Gen5 |
| scePthreadAttrGetstackaddr | `Ru36fiTtJzA` | Gen4, Gen5 |
| scePthreadAttrGetstacksize | `-fA+7ZlGDQs` | Gen4, Gen5 |
| scePthreadAttrInit | `nsYoNRywwNg` | Gen4, Gen5 |
| scePthreadAttrSetaffinity | `3qxgM4ezETA` | Gen4, Gen5 |
| scePthreadAttrSetdetachstate | `-Wreprtu0Qs` | Gen4, Gen5 |
| scePthreadAttrSetguardsize | `El+cQ20DynU` | Gen4, Gen5 |
| scePthreadAttrSetinheritsched | `eXbUSpEaTsA` | Gen4, Gen5 |
| scePthreadAttrSetschedparam | `DzES9hQF4f4` | Gen4, Gen5 |
| scePthreadAttrSetschedpolicy | `4+h9EzwKF4I` | Gen4, Gen5 |
| scePthreadAttrSetstacksize | `UTXzJbWhhTE` | Gen4, Gen5 |
| scePthreadCondBroadcast | `JGgj7Uvrl+A` | Gen4, Gen5 |
| scePthreadCondDestroy | `g+PZd2hiacg` | Gen4, Gen5 |
| scePthreadCondInit | `2Tb92quprl0` | Gen4, Gen5 |
| scePthreadCondSignal | `kDh-NfxgMtE` | Gen4, Gen5 |
| scePthreadCondTimedwait | `BmMjYxmew1w` | Gen4, Gen5 |
| scePthreadCondWait | `WKAXJ4XBPQ4` | Gen4, Gen5 |
| scePthreadCondattrDestroy | `waPcxYiR3WA` | Gen4, Gen5 |
| scePthreadCondattrInit | `m5-2bsNfv7s` | Gen4, Gen5 |
| scePthreadCreate | `6UgtwV+0zb4` | Gen4, Gen5 |
| scePthreadDetach | `4qGrR6eoP9Y` | Gen4, Gen5 |
| scePthreadEqual | `3PtV6p3QNX4` | Gen4, Gen5 |
| scePthreadExit | `3kg7rT0NQIs` | Gen4, Gen5 |
| scePthreadGetaffinity | `rcrVFJsQWRY` | Gen4, Gen5 |
| scePthreadGetname | `How7B8Oet6k` | Gen4, Gen5 |
| scePthreadGetprio | `1tKyG7RlMJo` | Gen4, Gen5 |
| scePthreadGetspecific | `eoht7mQOCmo` | Gen4, Gen5 |
| scePthreadGetthreadid | `EI-5-jlq2dE` | Gen4, Gen5 |
| scePthreadJoin | `onNY9Byn-W8` | Gen4, Gen5 |
| scePthreadKeyCreate | `geDaqgH9lTg` | Gen4, Gen5 |
| scePthreadKeyDelete | `PrdHuuDekhY` | Gen4, Gen5 |
| scePthreadMutexDestroy | `2Of0f+3mhhE` | Gen4, Gen5 |
| scePthreadMutexInit | `cmo1RIYva9o` | Gen4, Gen5 |
| scePthreadMutexLock | `9UK1vLZQft4` | Gen4, Gen5 |
| scePthreadMutexTrylock | `upoVrzMHFeE` | Gen4, Gen5 |
| scePthreadMutexUnlock | `tn3VlD0hG60` | Gen4, Gen5 |
| scePthreadMutexattrDestroy | `smWEktiyyG0` | Gen4, Gen5 |
| scePthreadMutexattrInit | `F8bUHwAG284` | Gen4, Gen5 |
| scePthreadMutexattrSetprotocol | `1FGvU0i9saQ` | Gen4, Gen5 |
| scePthreadMutexattrSettype | `iMp8QpE+XO4` | Gen4, Gen5 |
| scePthreadOnce | `14bOACANTBo` | Gen4, Gen5 |
| scePthreadRwlockDestroy | `BB+kb08Tl9A` | Gen4, Gen5 |
| scePthreadRwlockInit | `6ULAa0fq4jA` | Gen4, Gen5 |
| scePthreadRwlockRdlock | `Ox9i0c7L5w0` | Gen4, Gen5 |
| scePthreadRwlockUnlock | `+L98PIbGttk` | Gen4, Gen5 |
| scePthreadRwlockWrlock | `mqdNorrB+gI` | Gen4, Gen5 |
| scePthreadSelf | `aI+OeCz8xrQ` | Gen4, Gen5 |
| scePthreadSetaffinity | `bt3CTBKmGyI` | Gen4, Gen5 |
| scePthreadSetprio | `W0Hpm2X0uPE` | Gen4, Gen5 |
| scePthreadSetspecific | `+BzXYkqYeLE` | Gen4, Gen5 |
| scePthreadYield | `T72hz6ffq08` | Gen4, Gen5 |
| sem_destroy | `__hle_sem_destroy` | Gen4, Gen5 |
| sem_getvalue | `__hle_sem_getvalue` | Gen4, Gen5 |
| sem_init | `__hle_sem_init` | Gen4, Gen5 |
| sem_post | `__hle_sem_post` | Gen4, Gen5 |
| sem_timedwait | `__hle_sem_timedwait` | Gen4, Gen5 |
| sem_trywait | `__hle_sem_trywait` | Gen4, Gen5 |
| sem_wait | `__hle_sem_wait` | Gen4, Gen5 |
| write | `FN4gaPmuFV8` | Gen4, Gen5 |

## libSceAgc (51)

| Export | NID | Target |
| --- | --- | --- |
| sceAgcAcbAcquireMem | `KT-hTp-Ch14` | Gen5 |
| sceAgcAcbDispatchIndirect | `j3EtxFkSIhQ` | Gen5 |
| sceAgcAcbEventWrite | `cFazmnXpJOE` | Gen5 |
| sceAgcAcbResetQueue | `JrtiDtKeS38` | Gen5 |
| sceAgcAcbWaitRegMem | `htn36gPnBk4` | Gen5 |
| sceAgcAcbWriteData | `eZ4+17OQz4Q` | Gen5 |
| sceAgcCbDispatch | `k3GhuSNmBLU` | Gen5 |
| sceAgcCbNop | `LtTouSCZjHM` | Gen5 |
| sceAgcCbReleaseMem | `wr23dPKyWc0` | Gen5 |
| sceAgcCbSetShRegisterRangeDirect | `n2fD4A+pb+g` | Gen5 |
| sceAgcCbSetShRegistersDirect | `UZbQjYAwwXM` | Gen5 |
| sceAgcCreateInterpolantMapping | `HV4j+E0MBHE` | Gen5 |
| sceAgcCreatePrimState | `D9sr1xGUriE` | Gen5 |
| sceAgcCreateShader | `f3dg2CSgRKY` | Gen5 |
| sceAgcDcbAcquireMem | `57labkp+rSQ` | Gen5 |
| sceAgcDcbDispatchIndirect | `CtB+A9-VxO0` | Gen5 |
| sceAgcDcbDmaData | `WmAc2MEj6Io` | Gen5 |
| sceAgcDcbDrawIndexAuto | `Yw0jKSqop+E` | Gen5 |
| sceAgcDcbDrawIndexOffset | `B+aG9DUnTKA` | Gen5 |
| sceAgcDcbEventWrite | `aJf+j5yntiU` | Gen5 |
| sceAgcDcbGetLodStats | `vuSXe69VILM` | Gen5 |
| sceAgcDcbGetLodStatsGetSize | `rUuVjyR+Rd4` | Gen5 |
| sceAgcDcbPopMarker | `H7uZqCoNuWk` | Gen5 |
| sceAgcDcbPushMarker | `+kSrjIVxKFE` | Gen5 |
| sceAgcDcbResetQueue | `TRO721eVt4g` | Gen5 |
| sceAgcDcbSetBaseIndirectArgs | `RmaJwLtc8rY` | Gen5 |
| sceAgcDcbSetCxRegistersIndirect | `ZvwO9euwYzc` | Gen5 |
| sceAgcDcbSetFlip | `YUeqkyT7mEQ` | Gen5 |
| sceAgcDcbSetIndexBuffer | `l4fM9K-Lyks` | Gen5 |
| sceAgcDcbSetIndexSize | `GIIW2J37e70` | Gen5 |
| sceAgcDcbSetNumInstances | `tSBxhAPyytQ` | Gen5 |
| sceAgcDcbSetShRegistersIndirect | `-HOOCn0JY48` | Gen5 |
| sceAgcDcbSetUcRegistersIndirect | `hvUfkUIQcOE` | Gen5 |
| sceAgcDcbWaitRegMem | `VmW0Tdpy420` | Gen5 |
| sceAgcDcbWaitUntilSafeForRendering | `MWiElSNE8j8` | Gen5 |
| sceAgcDcbWriteData | `i1jyy49AjXU` | Gen5 |
| sceAgcDmaDataPatchSetDstAddressOrOffset | `IxYiarKlXxM` | Gen5 |
| sceAgcGetDataPacketPayloadAddress | `V++UgBtQhn0` | Gen5 |
| sceAgcGetRegisterDefaults2 | `2JtWUUiYBXs` | Gen5 |
| sceAgcGetRegisterDefaults2Internal | `wRbq6ZjNop4` | Gen5 |
| sceAgcInit | `23LRUSvYu1M` | Gen5 |
| sceAgcQueueEndOfPipeActionPatchAddress | `0fWWK5uG9rQ` | Gen5 |
| sceAgcSetCxRegIndirectPatchAddRegisters | `d-6uF9sZDIU` | Gen5 |
| sceAgcSetCxRegIndirectPatchSetAddress | `vcmNN+AAXnY` | Gen5 |
| sceAgcSetShRegIndirectPatchAddRegisters | `z2duB-hHQSM` | Gen5 |
| sceAgcSetShRegIndirectPatchSetAddress | `Qrj4c+61z4A` | Gen5 |
| sceAgcSetUcRegIndirectPatchAddRegisters | `vRoArM9zaIk` | Gen5 |
| sceAgcSetUcRegIndirectPatchSetAddress | `6lNcCp+fxi4` | Gen5 |
| sceAgcSuspendPoint | `h9z6+0hEydk` | Gen5 |
| sceAgcUnknownQj7QZpgr9Uw | `qj7QZpgr9Uw` | Gen5 |
| sceAgcWaitRegMemPatchAddress | `3KDcnM3lrcU` | Gen5 |

## libSceAgcDriver (4)

| Export | NID | Target |
| --- | --- | --- |
| sceAgcDriverAddEqEvent | `w2rJhmD+dsE` | Gen5 |
| sceAgcDriverDeleteEqEvent | `DL2RXaXOy88` | Gen5 |
| sceAgcDriverSubmitAcb | `gSRnr79F8tQ` | Gen5 |
| sceAgcDriverSubmitDcb | `UglJIZjGssM` | Gen5 |

## libSceAmpr (15)

| Export | NID | Target |
| --- | --- | --- |
| sceAmprAprCommandBufferConstructor | `a8uLzYY--tM` | Gen5 |
| sceAmprAprCommandBufferDestructor | `Qs1xtplKo0U` | Gen5 |
| sceAmprAprCommandBufferReadFile | `mQ16-QdKv7k` | Gen5 |
| sceAmprCommandBufferClearBuffer | `ULvXMDz56po` | Gen5 |
| sceAmprCommandBufferConstructor | `8aI7R7WaOlc` | Gen5 |
| sceAmprCommandBufferDestructor | `GuchCTefuZw` | Gen5 |
| sceAmprCommandBufferGetCurrentOffset | `GnxKOHEawhk` | Gen5 |
| sceAmprCommandBufferGetSize | `tZDDEo2tE5k` | Gen5 |
| sceAmprCommandBufferReset | `baQO9ez2gL4` | Gen5 |
| sceAmprCommandBufferSetBuffer | `N-FSPA4S3nI` | Gen5 |
| sceAmprCommandBufferWriteAddressOnCompletion | `sJXyWHjP-F8` | Gen5 |
| sceAmprCommandBufferWriteKernelEventQueue_04_00 | `H896Pt-yB4I` | Gen5 |
| sceAmprMeasureCommandSizeReadFile | `vWU-odnS+fU` | Gen5 |
| sceAmprMeasureCommandSizeWriteAddressOnCompletion | `C+IEj+BsAFM` | Gen5 |
| sceAmprMeasureCommandSizeWriteKernelEventQueue_04_00 | `sSAUCCU1dv4` | Gen5 |

## libSceAppContent (4)

| Export | NID | Target |
| --- | --- | --- |
| sceAppContentAppParamGetInt | `99b82IKXpH4` | Gen4, Gen5 |
| sceAppContentGetAddcontInfoList | `xnd8BJzAxmk` | Gen4, Gen5 |
| sceAppContentInitialize | `R9lA82OraNs` | Gen4, Gen5 |
| sceAppContentTemporaryDataMount2 | `buYbeLOGWmA` | Gen4, Gen5 |

## libSceAudioOut (4)

| Export | NID | Target |
| --- | --- | --- |
| sceAudioOutClose | `__hle_audio_close` | Gen4, Gen5 |
| sceAudioOutInit | `__hle_audio_init` | Gen4, Gen5 |
| sceAudioOutOpen | `__hle_audio_open` | Gen4, Gen5 |
| sceAudioOutOutput | `__hle_audio_output` | Gen4, Gen5 |

## libSceAudioOut2 (11)

| Export | NID | Target |
| --- | --- | --- |
| sceAudioOut2ContextCreate | `0x6o1VVAYSY` | Gen5 |
| sceAudioOut2ContextDestroy | `on6ZH7Abo10` | Gen5 |
| sceAudioOut2ContextQueryMemory | `pDmme7Bgm6E` | Gen5 |
| sceAudioOut2ContextResetParam | `t5YrizufpQc` | Gen5 |
| sceAudioOut2GetSpeakerInfo | `DImz2Ft9E2g` | Gen5 |
| sceAudioOut2Initialize | `g2tViFIohHE` | Gen5 |
| sceAudioOut2PortCreate | `JK2wamZPzwM` | Gen5 |
| sceAudioOut2PortDestroy | `cd+Rtw+D1x8` | Gen5 |
| sceAudioOut2PortGetState | `gatEUKG+Ea4` | Gen5 |
| sceAudioOut2UserCreate | `xywYcRB7nbQ` | Gen5 |
| sceAudioOut2UserDestroy | `IaZXJ9M79uo` | Gen5 |

## libSceCommonDialog (2)

| Export | NID | Target |
| --- | --- | --- |
| sceCommonDialogInitialize | `uoUpLGNkygk` | Gen4, Gen5 |
| sceCommonDialogIsUsed | `BQ3tey0JmQM` | Gen4, Gen5 |

## libSceCoredump (1)

| Export | NID | Target |
| --- | --- | --- |
| sceCoredumpRegisterCoredumpHandler | `8zLSfEfW5AU` | Gen4, Gen5 |

## libSceFiber (15)

| Export | NID | Target |
| --- | --- | --- |
| _sceFiberAttachContextAndRun | `avfGJ94g36Q` | Gen4, Gen5 |
| _sceFiberAttachContextAndSwitch | `ZqhZFuzKT6U` | Gen4, Gen5 |
| _sceFiberGetThreadFramePointerAddress | `0dy4JtMUcMQ` | Gen4, Gen5 |
| _sceFiberInitializeImpl | `hVYD7Ou2pCQ` | Gen4, Gen5 |
| _sceFiberInitializeWithInternalOptionImpl | `7+OJIpko9RY` | Gen4, Gen5 |
| sceFiberFinalize | `JeNX5F-NzQU` | Gen4, Gen5 |
| sceFiberGetInfo | `uq2Y5BFz0PE` | Gen4, Gen5 |
| sceFiberGetSelf | `p+zLIOg27zU` | Gen4, Gen5 |
| sceFiberOptParamInitialize | `asjUJJ+aa8s` | Gen4, Gen5 |
| sceFiberRename | `JzyT91ucGDc` | Gen4, Gen5 |
| sceFiberReturnToThread | `B0ZX2hx9DMw` | Gen4, Gen5 |
| sceFiberRun | `a0LLrZWac0M` | Gen4, Gen5 |
| sceFiberStartContextSizeCheck | `Lcqty+QNWFc` | Gen4, Gen5 |
| sceFiberStopContextSizeCheck | `Kj4nXMpnM8Y` | Gen4, Gen5 |
| sceFiberSwitch | `PFT2S-tJ7Uk` | Gen4, Gen5 |

## libSceGameUpdate (1)

| Export | NID | Target |
| --- | --- | --- |
| sceGameUpdateInitialize | `YJtKLttI9fM` | Gen4, Gen5 |

## libSceHttp (4)

| Export | NID | Target |
| --- | --- | --- |
| sceHttpCreateTemplate | `0gYjPTR-6cY` | Gen4, Gen5 |
| sceHttpDeleteTemplate | `4I8vEpuEhZ8` | Gen4, Gen5 |
| sceHttpInit | `A9cVMUtEp4Y` | Gen4, Gen5 |
| sceHttpTerm | `Ik-KpLTlf7Q` | Gen4, Gen5 |

## libSceHttp2 (2)

| Export | NID | Target |
| --- | --- | --- |
| sceHttp2Init | `3JCe3lCbQ8A` | Gen4, Gen5 |
| sceHttp2Term | `YiBUtz-pGkc` | Gen4, Gen5 |

## libSceJson (5)

| Export | NID | Target |
| --- | --- | --- |
| _ZN3sce4Json11Initializer10initializeEPKNS0_13InitParameterE | `Cxwy7wHq4J0` | Gen4, Gen5 |
| _ZN3sce4Json11InitializerC1Ev | `cK6bYHf-Q5E` | Gen4, Gen5 |
| _ZN3sce4Json11InitializerD1Ev | `RujUxbr3haM` | Gen4, Gen5 |
| _ZN3sce4Json12MemAllocatorC2Ev | `-hJRce8wn1U` | Gen4, Gen5 |
| _ZN3sce4Json12MemAllocatorD2Ev | `OcAgPxcq5Vk` | Gen4, Gen5 |

## libSceKeyboard (4)

| Export | NID | Target |
| --- | --- | --- |
| sceKeyboardClose | `__hle_keyboard_close` | Gen4, Gen5 |
| sceKeyboardInit | `__hle_keyboard_init` | Gen4, Gen5 |
| sceKeyboardOpen | `__hle_keyboard_open` | Gen4, Gen5 |
| sceKeyboardReadState | `__hle_keyboard_read_state` | Gen4, Gen5 |

## libSceNet (7)

| Export | NID | Target |
| --- | --- | --- |
| sceNetInit | `Nlev7Lg8k3A` | Gen4, Gen5 |
| sceNetPoolCreate | `dgJBaeJnGpo` | Gen4, Gen5 |
| sceNetPoolDestroy | `K7RlrTkI-mw` | Gen4, Gen5 |
| sceNetResolverCreate | `C4UgDHHPvdw` | Gen4, Gen5 |
| sceNetResolverDestroy | `kJlYH5uMAWI` | Gen4, Gen5 |
| sceNetResolverGetError | `J5i3hiLJMPk` | Gen4, Gen5 |
| sceNetTerm | `cTGkc6-TBlI` | Gen4, Gen5 |

## libSceNetCtl (6)

| Export | NID | Target |
| --- | --- | --- |
| sceNetCtlCheckCallback | `iQw3iQPhvUQ` | Gen4, Gen5 |
| sceNetCtlGetInfo | `obuxdTiwkF8` | Gen4, Gen5 |
| sceNetCtlGetNatInfo | `JO4yuTuMoKI` | Gen4, Gen5 |
| sceNetCtlGetState | `uBPlr0lbuiI` | Gen4, Gen5 |
| sceNetCtlInit | `gky0+oaNM4k` | Gen4, Gen5 |
| sceNetCtlRegisterCallback | `UJ+Z7Q+4ck0` | Gen4, Gen5 |

## libSceNpEntitlementAccess (1)

| Export | NID | Target |
| --- | --- | --- |
| sceNpEntitlementAccessInitialize | `jO8DM8oyego` | Gen4, Gen5 |

## libSceNpGameIntent (1)

| Export | NID | Target |
| --- | --- | --- |
| sceNpGameIntentInitialize | `m87BHxt-H60` | Gen4, Gen5 |

## libSceNpManager (6)

| Export | NID | Target |
| --- | --- | --- |
| sceNpCheckCallback | `3Zl8BePTh9Y` | Gen4, Gen5 |
| sceNpCheckCallbackForLib | `JELHf4xPufo` | Gen4, Gen5 |
| sceNpGetState | `eQH7nWPcAgc` | Gen4, Gen5 |
| sceNpRegisterStateCallback | `VfRSmPmj8Q8` | Gen4, Gen5 |
| sceNpRegisterStateCallbackA | `qQJfO8HAiaY` | Gen4, Gen5 |
| sceNpSetNpTitleId | `Ec63y59l9tw` | Gen4, Gen5 |

## libSceNpManagerForToolkit (1)

| Export | NID | Target |
| --- | --- | --- |
| sceNpRegisterStateCallbackForToolkit | `0c7HbXRKUt4` | Gen4, Gen5 |

## libSceNpSessionSignaling (1)

| Export | NID | Target |
| --- | --- | --- |
| sceNpSessionSignalingInitialize | `ysmw6J-P8Ak` | Gen4, Gen5 |

## libSceNpUniversalDataSystem (4)

| Export | NID | Target |
| --- | --- | --- |
| sceNpUniversalDataSystemCreateContext | `5zBnau1uIEo` | Gen4, Gen5 |
| sceNpUniversalDataSystemCreateHandle | `hT0IAEvN+M0` | Gen4, Gen5 |
| sceNpUniversalDataSystemInitialize | `sjaobBgqeB4` | Gen4, Gen5 |
| sceNpUniversalDataSystemRegisterContext | `tpFJ8LIKvPw` | Gen4, Gen5 |

## libSceNpWebApi2 (2)

| Export | NID | Target |
| --- | --- | --- |
| sceNpWebApi2Initialize | `+o9816YQhqQ` | Gen4, Gen5 |
| sceNpWebApi2Terminate | `bEvXpcEk200` | Gen4, Gen5 |

## libScePad (10)

| Export | NID | Target |
| --- | --- | --- |
| scePadClose | `__hle_pad_close` | Gen4, Gen5 |
| scePadGetControllerInformation | `gjP9-KQzoUk` | Gen4, Gen5 |
| scePadInit | `hv1luiJrqQM` | Gen4, Gen5 |
| scePadOpen | `xk0AcarP3V4` | Gen4, Gen5 |
| scePadRead | `q1cHNfGycLI` | Gen4, Gen5 |
| scePadReadState | `YndgXqQVV7c` | Gen4, Gen5 |
| scePadSetLightBar | `__hle_pad_light_bar` | Gen4, Gen5 |
| scePadSetMotionSensorState | `clVvL4ZDntw` | Gen4, Gen5 |
| scePadSetVibration | `__hle_pad_vibration` | Gen4, Gen5 |
| scePadSetVibrationMode | `__hle_pad_vibration_mode` | Gen4, Gen5 |

## libScePlayGo (15)

| Export | NID | Target |
| --- | --- | --- |
| scePlayGoClose | `Uco1I0dlDi8` | Gen4, Gen5 |
| scePlayGoGetChunkId | `73fF1MFU8hA` | Gen4, Gen5 |
| scePlayGoGetEta | `v6EZ-YWRdMs` | Gen4, Gen5 |
| scePlayGoGetInstallSpeed | `rvBSfTimejE` | Gen4, Gen5 |
| scePlayGoGetLanguageMask | `3OMbYZBaa50` | Gen4, Gen5 |
| scePlayGoGetLocus | `uWIYLFkkwqk` | Gen4, Gen5 |
| scePlayGoGetProgress | `-RJWNMK3fC8` | Gen4, Gen5 |
| scePlayGoGetToDoList | `Nn7zKwnA5q0` | Gen4, Gen5 |
| scePlayGoInitialize | `ts6GlZOKRrE` | Gen4, Gen5 |
| scePlayGoOpen | `M1Gma1ocrGE` | Gen4, Gen5 |
| scePlayGoPrefetch | `-Q1-u1a7p0g` | Gen4, Gen5 |
| scePlayGoSetInstallSpeed | `4AAcTU9R3XM` | Gen4, Gen5 |
| scePlayGoSetLanguageMask | `LosLlHOpNqQ` | Gen4, Gen5 |
| scePlayGoSetToDoList | `gUPGiOQ1tmQ` | Gen4, Gen5 |
| scePlayGoTerminate | `MPe0EeBGM-E` | Gen4, Gen5 |

## libScePosix (1)

| Export | NID | Target |
| --- | --- | --- |
| pthread_exit | `FJrT5LuUBAU` | Gen4, Gen5 |

## libSceRtc (4)

| Export | NID | Target |
| --- | --- | --- |
| sceRtcGetCurrentClockLocalTime | `ZPD1YOKI+Kw` | Gen4, Gen5 |
| sceRtcGetCurrentTick | `18B2NS1y9UU` | Gen4, Gen5 |
| sceRtcGetTick | `8w-H19ip48I` | Gen4, Gen5 |
| sceRtcSetTick | `ueega6v3GUw` | Gen4, Gen5 |

## libSceSaveData (2)

| Export | NID | Target |
| --- | --- | --- |
| sceSaveDataDirNameSearch | `dyIhnXq-0SM` | Gen4, Gen5 |
| sceSaveDataInitialize3 | `TywrFKCoLGY` | Gen4, Gen5 |

## libSceShareUtility (2)

| Export | NID | Target |
| --- | --- | --- |
| sceShareInitialize | `nBDD66kiFW8` | Gen4, Gen5 |
| sceShareSetContentParam | `7QZtURYnXG4` | Gen4, Gen5 |

## libSceSsl (3)

| Export | NID | Target |
| --- | --- | --- |
| sceSslClose | `viRXSHZYd0c` | Gen4, Gen5 |
| sceSslInit | `hdpVEUDFW3s` | Gen4, Gen5 |
| sceSslTerm | `0K1yQ6Lv-Yc` | Gen4, Gen5 |

## libSceSysmodule (4)

| Export | NID | Target |
| --- | --- | --- |
| sceSysmoduleIsLoaded | `fMP5NHUOaMk` | Gen4, Gen5 |
| sceSysmoduleLoadModule | `g8cM39EUZ6o` | Gen4, Gen5 |
| sceSysmoduleLoadModuleInternalWithArg | `hHrGoGoNf+s` | Gen4, Gen5 |
| sceSysmoduleUnloadModule | `eR2bZFAAU0Q` | Gen4, Gen5 |

## libSceSystemGesture (6)

| Export | NID | Target |
| --- | --- | --- |
| sceSystemGestureCreateTouchRecognizer | `FWF8zkhr854` | Gen4, Gen5 |
| sceSystemGestureGetTouchEventsCount | `h8uongcBNVs` | Gen4, Gen5 |
| sceSystemGestureInitializePrimitiveTouchRecognizer | `3pcAvmwKCvM` | Gen4, Gen5 |
| sceSystemGestureOpen | `qpo-mEOwje0` | Gen4, Gen5 |
| sceSystemGestureUpdatePrimitiveTouchRecognizer | `GgFMb22sbbI` | Gen4, Gen5 |
| sceSystemGestureUpdateTouchRecognizer | `j4h82CQWENo` | Gen4, Gen5 |

## libSceSystemService (4)

| Export | NID | Target |
| --- | --- | --- |
| sceSystemServiceGetDisplaySafeAreaInfo | `1n37q1Bvc5Y` | Gen4, Gen5 |
| sceSystemServiceGetStatus | `rPo6tV8D9bM` | Gen4, Gen5 |
| sceSystemServiceHideSplashScreen | `Vo5V8KAwCmk` | Gen4, Gen5 |
| sceSystemServiceParamGetInt | `fZo48un7LK4` | Gen4, Gen5 |

## libSceUserService (7)

| Export | NID | Target |
| --- | --- | --- |
| sceUserServiceGetEvent | `yH17Q6NWtVg` | Gen4, Gen5 |
| sceUserServiceGetForegroundUser | `__hle_foreground_user` | Gen4, Gen5 |
| sceUserServiceGetInitialUser | `CdWp0oHWGr0` | Gen4, Gen5 |
| sceUserServiceGetLoginUserIdList | `fPhymKNvK-A` | Gen4, Gen5 |
| sceUserServiceGetPlatformPrivacySetting | `D-CzAxQL0XI` | Gen4, Gen5 |
| sceUserServiceGetUserName | `1xxcMiGu2fo` | Gen4, Gen5 |
| sceUserServiceInitialize | `j3YMu1MVNNo` | Gen4, Gen5 |

## libSceVideoOut (18)

| Export | NID | Target |
| --- | --- | --- |
| sceVideoOutAddFlipEvent | `HXzjK9yI30k` | Gen4, Gen5 |
| sceVideoOutAdjustColor_ | `pv9CI5VC+R0` | Gen4, Gen5 |
| sceVideoOutClose | `uquVH4-Du78` | Gen4, Gen5 |
| sceVideoOutColorSettingsSetGamma_ | `DYhhWbJSeRg` | Gen4, Gen5 |
| sceVideoOutGetEventData | `rWUTcKdkUzQ` | Gen4, Gen5 |
| sceVideoOutGetEventId | `U2JJtSqNKZI` | Gen4, Gen5 |
| sceVideoOutGetOutputStatus | `utPrVdxio-8` | Gen4, Gen5 |
| sceVideoOutIsFlipPending | `zgXifHT9ErY` | Gen4, Gen5 |
| sceVideoOutOpen | `Up36PTk687E` | Gen4, Gen5 |
| sceVideoOutRegisterBuffers | `w3BY+tAEiQY` | Gen4, Gen5 |
| sceVideoOutRegisterBuffers2 | `rKBUtgRrtbk` | Gen4, Gen5 |
| sceVideoOutSetBufferAttribute | `i6-sR91Wt-4` | Gen4, Gen5 |
| sceVideoOutSetBufferAttribute2 | `PjS5uASwcV8` | Gen4, Gen5 |
| sceVideoOutSetFlipRate | `CBiu4mCE1DA` | Gen4, Gen5 |
| sceVideoOutSetWindowModeMargins | `MTxxrOCeSig` | Gen4, Gen5 |
| sceVideoOutSubmitFlip | `U46NwOiJpys` | Gen4, Gen5 |
| sceVideoOutUnregisterBuffers | `N5KDtkIjjJ4` | Gen4, Gen5 |
| sceVideoOutWaitVblank | `j6RaAUlaLv0` | Gen4, Gen5 |
