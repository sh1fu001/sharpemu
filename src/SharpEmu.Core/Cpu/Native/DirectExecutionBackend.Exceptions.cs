// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SharpEmu.Core.Cpu.Disasm;
using SharpEmu.HLE;

namespace SharpEmu.Core.Cpu.Native;

public sealed partial class DirectExecutionBackend
{
	private const ulong LazyCommitWindowBytes = 0x0001_0000UL;
	private static int _lazyCommitTraceCount;

	// Opt-in (SHARPEMU_SKIP_GARBAGE_STORES=1) "best-effort keep running" mode: when guest code stores
	// to a genuinely inaccessible address (a garbage index computed from an incomplete-VM value), step
	// over the store instead of letting the uncatchable AV kill the process. The store targeted junk
	// memory anyway, so dropping it loses nothing; it keeps a non-essential garbage write (e.g. a
	// debug/name string) from taking down the whole run.
	private bool _skipGarbageStores;
	private long _garbageStoreSkips;

	private unsafe void SetupExceptionHandler()
	{
		_skipGarbageStores = string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_SKIP_GARBAGE_STORES"), "1", StringComparison.Ordinal);
		if (_skipGarbageStores)
		{
			Console.Error.WriteLine("[LOADER][INFO] Garbage-store survival mode enabled (SHARPEMU_SKIP_GARBAGE_STORES=1).");
		}

		StartWatchWritesArmingThread();
		StartGuestBreakpointArmingThread();

		if (!string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_DISABLE_RAW_HANDLER"), "1", StringComparison.Ordinal))
		{
			_rawExceptionHandlerStub = CreateExceptionHandlerTrampoline(RawVectoredHandlerPtrManaged);
			if (_rawExceptionHandlerStub == 0)
			{
				throw new InvalidOperationException("Failed to create raw exception handler trampoline");
			}
			_rawExceptionHandler = (nint)AddVectoredExceptionHandler(1u, _rawExceptionHandlerStub);
			Console.Error.WriteLine($"[LOADER][INFO] Raw exception handler installed: 0x{_rawExceptionHandler:X16}");
		}
		else
		{
			Console.Error.WriteLine("[LOADER][INFO] Raw exception handler disabled by SHARPEMU_DISABLE_RAW_HANDLER=1");
		}

		_handlerDelegate = VectoredHandler;
		_handlerHandle = GCHandle.Alloc(_handlerDelegate);
		_exceptionHandlerStub = CreateExceptionHandlerTrampoline(Marshal.GetFunctionPointerForDelegate(_handlerDelegate));
		if (_exceptionHandlerStub == 0)
		{
			throw new InvalidOperationException("Failed to create exception handler trampoline");
		}
		_exceptionHandler = (nint)AddVectoredExceptionHandler(1u, _exceptionHandlerStub);
		Console.Error.WriteLine($"[LOADER][INFO] Exception handler installed: 0x{_exceptionHandler:X16}");

		_unhandledFilterDelegate = UnhandledExceptionFilter;
		_unhandledFilterHandle = GCHandle.Alloc(_unhandledFilterDelegate);
		_unhandledFilterStub = CreateExceptionHandlerTrampoline(Marshal.GetFunctionPointerForDelegate(_unhandledFilterDelegate));
		if (_unhandledFilterStub == 0)
		{
			throw new InvalidOperationException("Failed to create unhandled exception filter trampoline");
		}
		SetUnhandledExceptionFilter(_unhandledFilterStub);
	}

	private unsafe int UnhandledExceptionFilter(void* exceptionInfo)
	{
		try
		{
			EXCEPTION_RECORD* exceptionRecord = ((EXCEPTION_POINTERS*)exceptionInfo)->ExceptionRecord;
			ulong rip = ReadCtxU64(((EXCEPTION_POINTERS*)exceptionInfo)->ContextRecord, 248);
			ulong rsp = ReadCtxU64(((EXCEPTION_POINTERS*)exceptionInfo)->ContextRecord, 152);
			Console.Error.WriteLine("[LOADER][FATAL] Unhandled exception filter fired.");
			Console.Error.WriteLine($"[LOADER][FATAL]   Code: 0x{exceptionRecord->ExceptionCode:X8}");
			Console.Error.WriteLine($"[LOADER][FATAL]   Exception Address: 0x{(ulong)(nint)exceptionRecord->ExceptionAddress:X16}");
			Console.Error.WriteLine($"[LOADER][FATAL]   RIP: 0x{rip:X16}");
			Console.Error.WriteLine($"[LOADER][FATAL]   RSP: 0x{rsp:X16}");
			Console.Error.Flush();
		}
		catch
		{
		}

		return 0;
	}

	private unsafe int VectoredHandler(void* exceptionInfo)
	{
		if (_vectoredHandlerDepth > 0)
		{
			LogNestedVectoredException(exceptionInfo);
			Console.Error.Flush();
			return 0;
		}

		_vectoredHandlerDepth++;
		try
		{
			EXCEPTION_RECORD* exceptionRecord = ((EXCEPTION_POINTERS*)exceptionInfo)->ExceptionRecord;
			uint exceptionCode = exceptionRecord->ExceptionCode;
			uint exceptionFlags = exceptionRecord->ExceptionFlags;
			ulong exceptionAddress = (ulong)exceptionRecord->ExceptionAddress;
			void* contextRecord = ((EXCEPTION_POINTERS*)exceptionInfo)->ContextRecord;
			if (contextRecord == null)
			{
				Console.Error.WriteLine("[LOADER][FATAL] ContextRecord is null!");
				Console.Error.Flush();
				return 0;
			}

			ulong rip = ReadCtxU64(contextRecord, 248);
			ulong rsp = ReadCtxU64(contextRecord, 152);

			// Guest breakpoints and the write-watchpoint hooks must run first. Both use a single-step
			// (TF) trap to advance one instruction; the breakpoint re-patch is checked before the
			// watchpoint completion because they share the 0x80000004 single-step exception.
			if (exceptionCode == 0x80000003u && TryHandleGuestBreakpoint(contextRecord, rip))
			{
				return -1;
			}
			if (exceptionCode == 0x80000004u && TryCompleteGuestBreakpointStep(contextRecord))
			{
				return -1;
			}
			if (exceptionCode == 3221225477u && TryHandleWatchedWriteFault(exceptionRecord, contextRecord, rip))
			{
				return -1;
			}
			if (exceptionCode == 0x80000004u && TryCompleteWatchedWriteStep(contextRecord))
			{
				return -1;
			}

			if (exceptionCode == 3221225477u && TryHandleLazyCommittedPage(exceptionRecord, contextRecord, rip, rsp))
			{
				return -1;
			}
			if (exceptionCode == 3221225477u && TryRecoverGuestExecuteFault(exceptionInfo) != 0)
			{
				return -1;
			}
			if (exceptionCode == 3221225477u && TryRecoverGuestGarbageStore(exceptionRecord, contextRecord, rip))
			{
				return -1;
			}
			if (exceptionCode == 3221225477u && TryRecoverGuestRunawayCopy(exceptionRecord, contextRecord, rip))
			{
				return -1;
			}
			if (IsBenignHostDebugException(exceptionCode))
			{
				return -1;
			}
			if (exceptionCode == MSVC_CPP_EXCEPTION)
			{
				return 0;
			}
			if (exceptionCode == 3221225501u && TryHandleGuestUd2(contextRecord, rip))
			{
				return -1;
			}
			if (exceptionCode == 0xC0000094u && TryHandleGuestDivideByZero(contextRecord, rip))
			{
				return -1;
			}

			switch (exceptionCode)
			{
				case 3221225477u:
					LogAccessViolationTrace(exceptionAddress, exceptionRecord);
					break;
				case 3221226505u:
					{
						ulong p0 = exceptionRecord->NumberParameters >= 1 ? (*exceptionRecord->ExceptionInformation) : 0;
						ulong p1 = exceptionRecord->NumberParameters >= 2 ? exceptionRecord->ExceptionInformation[1] : 0;
						Console.Error.WriteLine($"[LOADER][TRACE] VEH_FASTFAIL code=0x{exceptionCode:X8} ex=0x{exceptionAddress:X16} rip=0x{rip:X16} rsp=0x{rsp:X16} p0=0x{p0:X16} p1=0x{p1:X16}");
						Console.Error.Flush();
						break;
					}
			}

			ulong rax = ReadCtxU64(contextRecord, 120);
			ulong rbx = ReadCtxU64(contextRecord, 144);
			ulong rcx = ReadCtxU64(contextRecord, 128);
			ulong rdx = ReadCtxU64(contextRecord, 136);
			ulong rsi = ReadCtxU64(contextRecord, 168);
			ulong rdi = ReadCtxU64(contextRecord, 176);
			ulong rbp = ReadCtxU64(contextRecord, 160);
			ulong r8 = ReadCtxU64(contextRecord, 184);
			ulong r9 = ReadCtxU64(contextRecord, 192);
			ulong r10 = ReadCtxU64(contextRecord, 200);
			ulong r11 = ReadCtxU64(contextRecord, 208);
			ulong r12 = ReadCtxU64(contextRecord, 216);
			ulong r13 = ReadCtxU64(contextRecord, 224);
			ulong r14 = ReadCtxU64(contextRecord, 232);
			ulong r15 = ReadCtxU64(contextRecord, 240);

			Console.Error.WriteLine("[LOADER][INFO] =========================================");
			Console.Error.WriteLine("[LOADER][INFO] NATIVE EXCEPTION CAUGHT!");
			Console.Error.WriteLine($"[LOADER][INFO]   Code: 0x{exceptionCode:X8}");
			Console.Error.WriteLine($"[LOADER][INFO]   Exception Address: 0x{exceptionAddress:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RIP: 0x{rip:X16}");
			if (TryFormatNearestRuntimeSymbol(rip, out string symbol))
			{
				Console.Error.WriteLine("[LOADER][INFO]   RIP symbol: " + symbol);
			}
			Console.Error.WriteLine($"[LOADER][INFO]   RAX: 0x{rax:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RBX: 0x{rbx:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RCX: 0x{rcx:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RDX: 0x{rdx:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RSI: 0x{rsi:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RDI: 0x{rdi:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RBP: 0x{rbp:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   RSP: 0x{rsp:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R8 : 0x{r8:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R9 : 0x{r9:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R10: 0x{r10:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R11: 0x{r11:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R12: 0x{r12:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R13: 0x{r13:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R14: 0x{r14:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   R15: 0x{r15:X16}");
			Console.Error.WriteLine($"[LOADER][INFO]   Flags: 0x{exceptionFlags:X8}");

			ulong accessType = 0;
			ulong target = 0;
			if (exceptionCode == 3221225477u && exceptionRecord->NumberParameters >= 2)
			{
				accessType = *exceptionRecord->ExceptionInformation;
				target = exceptionRecord->ExceptionInformation[1];
				string accessText = accessType switch
				{
					0uL => "read",
					1uL => "write",
					8uL => "execute",
					_ => $"unknown({accessType})"
				};
				Console.Error.WriteLine("[LOADER][INFO]   AV access: " + accessText);
				Console.Error.WriteLine($"[LOADER][INFO]   AV target: 0x{target:X16}");
				if (VirtualQuery((void*)target, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) != 0)
				{
					Console.Error.WriteLine($"[LOADER][INFO]   AV target region: base=0x{mbi.BaseAddress:X16} size=0x{mbi.RegionSize:X16} state=0x{mbi.State:X08} protect=0x{mbi.Protect:X08}");
				}

			}

			try
			{
				Console.Error.WriteLine("[LOADER][INFO]   Stack qwords (RSP..):");
				for (int i = 0; i < 16; i++)
				{
					ulong stackAddr = rsp + (ulong)(i * 8);
					ulong value = (ulong)Marshal.ReadInt64((nint)stackAddr);
					Console.Error.WriteLine($"[LOADER][INFO]     [rsp+0x{i * 8:X2}] @0x{stackAddr:X16} = 0x{value:X16}");
				}
			}
			catch
			{
				Console.Error.WriteLine("[LOADER][WARNING]   Could not read stack qwords.");
			}

			try
			{
				Console.Error.WriteLine("[LOADER][INFO]   Frame chain (RBP walk):");
				ulong frame = rbp;
				for (int i = 0; i < 12; i++)
				{
					if (frame < 140733193388032L || frame > 140737488355327L)
					{
						break;
					}
					ulong next = (ulong)Marshal.ReadInt64((nint)frame);
					ulong ret = (ulong)Marshal.ReadInt64((nint)(frame + 8));
					string extra = TryFormatNearestRuntimeSymbol(ret, out string retSym) ? $" [{retSym}]" : string.Empty;
					Console.Error.WriteLine($"[LOADER][INFO]     frame#{i}: rbp=0x{frame:X16} ret=0x{ret:X16}{extra} next=0x{next:X16}");
					if (next <= frame)
					{
						break;
					}
					frame = next;
				}
			}
			catch
			{
				Console.Error.WriteLine("[LOADER][WARNING]   Could not walk RBP frame chain.");
			}

			switch (exceptionCode)
			{
				case 3221225477u:
					Console.Error.WriteLine("[LOADER][ERROR]   Type: Access Violation");
					Console.Error.WriteLine("[LOADER][ERROR]   This usually means:");
					Console.Error.WriteLine("[LOADER][ERROR]     - Guest code called an unmapped import");
					Console.Error.WriteLine("[LOADER][ERROR]     - Guest code accessed unmapped memory");
					Console.Error.WriteLine("[LOADER][ERROR]     - Need to implement HLE for this NID");
					try
					{
						// Each read is gated on page readability: a fault RIP can sit at the very
						// start of a 128-byte trampoline allocation, where RIP-16 lands on an
						// unmapped page and a raw read would kill the crash handler itself.
						if (TryReadHostBytes(rip, 16, out var code))
						{
							Console.Error.WriteLine("[LOADER][INFO]   Code at RIP: " + BitConverter.ToString(code).Replace("-", " "));
							if (code.Length >= 2 && code[0] == 0xF3 && (code[1] == 0xA4 || code[1] == 0xA5))
							{
								// rep movsb/movsd memcpy thunk: reconstruct the original call.
								var copied = rdi - rax;
								Console.Error.WriteLine(
									$"[LOADER][INFO]   memcpy forensics: dest_start=0x{rax:X16} copied=0x{copied:X16} " +
									$"src_start=0x{rsi - copied:X16} remaining=0x{rcx:X16}");
							}
							if (code[0] == 100)
							{
								Console.Error.WriteLine("[LOADER][ERROR]   Detected FS segment prefix - TLS access not patched!");
							}
							else if (code[0] == 101)
							{
								Console.Error.WriteLine("[LOADER][ERROR]   Detected GS segment prefix - TLS access not patched!");
							}
							else if (code[0] == 197 || code[0] == 196)
							{
								Console.Error.WriteLine("[LOADER][INFO]   Detected AVX instruction - check CPU support!");
								Console.Error.WriteLine($"[LOADER][INFO]   RBP: 0x{rbp:X16} (mod 16 = {rbp % 16})");
								Console.Error.WriteLine($"[LOADER][INFO]   RSP: 0x{rsp:X16} (mod 16 = {rsp % 16})");
							}
						}
						if (rip > 16 && TryReadHostBytes(rip - 16, 16, out var before))
						{
							Console.Error.WriteLine("[LOADER][INFO]   Code before RIP: " + BitConverter.ToString(before).Replace("-", " "));
						}
						if (rip > 32 && TryReadHostBytes(rip - 32, 64, out var window))
						{
							Console.Error.WriteLine("[LOADER][INFO]   Code window [RIP-0x20..]: " + BitConverter.ToString(window).Replace("-", " "));
						}
					}
					catch
					{
						Console.Error.WriteLine("[LOADER][ERROR]   Could not read code at RIP");
					}
					DumpRecentImportTrace();
					DumpGuestDisasmDiagnostics(rip, rbp);
					DumpGuestReferenceDiagnostics();
					DumpGuestPointerWindowDiagnostics();
					break;
				case 0xC0000094u:
					Console.Error.WriteLine("[LOADER][ERROR]   Type: Integer Division by Zero");
					try
					{
						if (TryReadHostBytes(rip, 16, out var divCode))
						{
							Console.Error.WriteLine("[LOADER][INFO]   Code at RIP: " + BitConverter.ToString(divCode).Replace("-", " "));
						}
						if (rip > 48 && TryReadHostBytes(rip - 48, 80, out var divWindow))
						{
							Console.Error.WriteLine("[LOADER][INFO]   Code window [RIP-0x30..]: " + BitConverter.ToString(divWindow).Replace("-", " "));
						}
					}
					catch
					{
						Console.Error.WriteLine("[LOADER][ERROR]   Could not read code at RIP");
					}
					DumpRecentImportTrace();
					DumpGuestDisasmDiagnostics(rip, rbp);
					DumpGuestPointerWindowDiagnostics();
					DumpGuestByteScanDiagnostics();
					break;
				case 2147483651u:
					Console.Error.WriteLine("[LOADER][WARNING]   Type: Breakpoint (int3)");
					Console.Error.WriteLine("[LOADER][WARNING]   Unexpected breakpoint in direct-bridge mode");
					break;
				case 3221225501u:
					Console.Error.WriteLine("[LOADER][INFO]   Type: Illegal Instruction");
					break;
			}

			Console.Error.WriteLine("[LOADER][INFO] =========================================");
			Console.Error.Flush();
			return 0;
		}
		finally
		{
			_vectoredHandlerDepth--;
		}
	}

	private unsafe bool TryHandleGuestUd2(void* contextRecord, ulong rip)
	{
		var hostExit = ActiveEntryReturnSentinelRip;
		if (hostExit < 65536)
		{
			return false;
		}

		try
		{
			var instruction = (byte*)rip;
			if (instruction[0] != 0x0F || instruction[1] != 0x0B)
			{
				return false;
			}

			WriteCtxU64(contextRecord, CTX_RAX, unchecked((ulong)-1L));
			WriteCtxU64(contextRecord, 248, hostExit);
			ActiveForcedGuestExit = true;
			LastError = $"Guest executed UD2 at 0x{rip:X16}.";
			Console.Error.WriteLine(
				$"[LOADER][WARN] Guest UD2 at 0x{rip:X16}; returning control to the host.");
			return true;
		}
		catch
		{
			return false;
		}
	}

	// ---- Write watchpoint (SHARPEMU_WATCH_WRITES=<hexaddr>[:<hexlen>]) --------------------------
	// Page-protection watchpoint that logs every writer (guest or host) of a small guest range.
	// The containing page is kept PAGE_READONLY once it exists; a write AV unprotects the page and
	// single-steps (TF) the faulting instruction, then the written value is logged and the page
	// re-protected. An event cap disarms the watch so a bulk copy sweeping through the page (e.g.
	// a runaway memcpy) cannot stall the run.

	private const int WatchWritesMaxEvents = 256;
	private const int WatchWritesMaxSteps = 4_000_000;
	private static int _watchWritesSteps;
	private const uint WatchPageReadOnly = 0x02;
	private const uint WatchPageReadWrite = 0x04;
	private const int CtxEFlagsOffset = 0x44;
	private const uint EFlagsTrapFlag = 0x100;

	private static readonly (ulong Address, ulong Length) _watchWrites = ParseWatchWrites();
	// SHARPEMU_WATCH_FF=1: only log/count in-range writes whose stored qword carries 0xFFFFFFFF in
	// either 32-bit half. Lets a whole-page watch pinpoint the writer of a (uint)-1 sentinel without
	// drowning in the page's ordinary traffic.
	private static readonly bool _watchWritesOnlyFF =
		string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_WATCH_FF"), "1", StringComparison.Ordinal);
	private static int _watchWritesArmed; // 0 = waiting for the page, 1 = armed, 2 = disarmed
	private static int _watchWritesEvents;

	[ThreadStatic] private static bool _watchWritesStepPending;
	[ThreadStatic] private static ulong _watchWritesStepTarget;
	[ThreadStatic] private static ulong _watchWritesStepRip;
	[ThreadStatic] private static ulong _watchWritesStepPage;

	private static ulong WatchWritesFirstPage => _watchWrites.Address & ~0xFFFUL;
	private static ulong WatchWritesLastPage => (_watchWrites.Address + _watchWrites.Length - 1) & ~0xFFFUL;

	private static (ulong Address, ulong Length) ParseWatchWrites()
	{
		var raw = Environment.GetEnvironmentVariable("SHARPEMU_WATCH_WRITES");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return (0, 0);
		}

		// '+' and ';' are accepted alongside ':' because MSYS shells path-convert colon-separated
		// environment values before they reach the process.
		var parts = raw.Split([':', ';', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var addressText = parts[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parts[0][2..] : parts[0];
		if (!ulong.TryParse(addressText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) ||
			address == 0)
		{
			Console.Error.WriteLine($"[LOADER][WARN] SHARPEMU_WATCH_WRITES value '{raw}' could not be parsed; watch disabled.");
			return (0, 0);
		}

		ulong length = 8;
		if (parts.Length > 1)
		{
			var lengthText = parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parts[1][2..] : parts[1];
			if (ulong.TryParse(lengthText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedLength) &&
				parsedLength > 0)
			{
				length = parsedLength;
			}
		}

		return (address, length);
	}

	// ---- Guest breakpoints (SHARPEMU_GUEST_BREAKPOINTS=<hexaddr>[,<hexaddr>...]) -----------------
	// Patches int3 (0xCC) over the first byte of each listed guest code address. On hit, logs the
	// integer-argument registers (System V order: rdi, rsi, rdx, rcx, r8, r9 + rax) and the call
	// depth, restores the byte, single-steps over the original instruction, then re-patches. This is
	// the general "instrument any internal game function" tool the serialization trace needs.
	private static readonly ulong[] _guestBreakpoints = ParseGuestBreakpoints();
	// SHARPEMU_GUEST_BP_MATCH="<reg>,<hexlo>,<hexhi>" restricts logging to hits where the named
	// integer register is in [lo,hi] — the only way to catch the one faulting call to an otherwise
	// hot function (e.g. the string reader whose cursor lands in the crash region).
	private static readonly (int CtxOffset, ulong Lo, ulong Hi) _guestBreakpointMatch = ParseGuestBreakpointMatch();
	private const int GuestBreakpointMaxHitsLogged = 200;
	private static readonly Dictionary<ulong, byte> _guestBreakpointOriginal = new();
	private static readonly Dictionary<ulong, int> _guestBreakpointHits = new();
	private static int _guestBreakpointsArmed;
	private static readonly object _guestBreakpointGate = new();

	[ThreadStatic] private static ulong _guestBreakpointStepAddress;

	private static ulong[] ParseGuestBreakpoints()
	{
		var raw = Environment.GetEnvironmentVariable("SHARPEMU_GUEST_BREAKPOINTS");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return Array.Empty<ulong>();
		}

		var result = new List<ulong>();
		foreach (var token in raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var text = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
			if (ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) && address > 0x10000)
			{
				result.Add(address);
			}
		}

		return result.ToArray();
	}

	private static (int CtxOffset, ulong Lo, ulong Hi) ParseGuestBreakpointMatch()
	{
		var raw = Environment.GetEnvironmentVariable("SHARPEMU_GUEST_BP_MATCH");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return (-1, 0, 0);
		}

		var parts = raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 3)
		{
			return (-1, 0, 0);
		}

		var offset = parts[0].ToLowerInvariant() switch
		{
			"rax" => CTX_RAX,
			"rcx" => CTX_RCX,
			"rdx" => 136,
			"rbx" => 144,
			"rsi" => CTX_RSI,
			"rdi" => CTX_RDI,
			"r8" => 184,
			"r9" => 192,
			"r12" => 216,
			"r13" => 224,
			"r14" => 232,
			"r15" => 240,
			_ => -1,
		};

		static ulong Hex(string s) => ulong.TryParse(
			s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s,
			NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;

		return offset < 0 ? (-1, 0, 0) : (offset, Hex(parts[1]), Hex(parts[2]));
	}

	private static void StartGuestBreakpointArmingThread()
	{
		if (_guestBreakpoints.Length == 0)
		{
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO] guest breakpoints requested: {string.Join(", ", Array.ConvertAll(_guestBreakpoints, a => $"0x{a:X}"))}; arming once code is mapped.");
		var thread = new Thread(static () =>
		{
			while (Volatile.Read(ref _guestBreakpointsArmed) < _guestBreakpoints.Length)
			{
				TryArmGuestBreakpoints();
				Thread.Sleep(25);
			}
		})
		{
			IsBackground = true,
			Name = "sharpemu-guest-breakpoints",
		};
		thread.Start();
	}

	private static unsafe void TryArmGuestBreakpoints()
	{
		lock (_guestBreakpointGate)
		{
			foreach (var address in _guestBreakpoints)
			{
				if (_guestBreakpointOriginal.ContainsKey(address))
				{
					continue;
				}

				if (VirtualQuery((void*)address, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
					mbi.State != MEM_COMMIT ||
					!IsExecutableProtection(mbi.Protect))
				{
					continue;
				}

				uint oldProtect;
				if (!VirtualProtect((void*)address, 1, 0x40u /* RWX */, &oldProtect))
				{
					continue;
				}

				_guestBreakpointOriginal[address] = *(byte*)address;
				*(byte*)address = 0xCC;
				uint restore;
				VirtualProtect((void*)address, 1, oldProtect, &restore);
				Interlocked.Increment(ref _guestBreakpointsArmed);
				Console.Error.WriteLine($"[LOADER][WARN] guest breakpoint ARMED at 0x{address:X16}.");
				Console.Error.Flush();
			}
		}
	}

	private unsafe bool TryHandleGuestBreakpoint(void* contextRecord, ulong rip)
	{
		byte original;
		lock (_guestBreakpointGate)
		{
			if (!_guestBreakpointOriginal.TryGetValue(rip, out original))
			{
				return false;
			}
		}

		var matches = true;
		if (_guestBreakpointMatch.CtxOffset >= 0)
		{
			var value = ReadCtxU64(contextRecord, _guestBreakpointMatch.CtxOffset);
			matches = value >= _guestBreakpointMatch.Lo && value <= _guestBreakpointMatch.Hi;
		}

		var hit = 0;
		if (matches)
		{
			lock (_guestBreakpointGate)
			{
				_guestBreakpointHits.TryGetValue(rip, out hit);
				_guestBreakpointHits[rip] = hit + 1;
			}
		}

		if (matches && hit < GuestBreakpointMaxHitsLogged)
		{
			var rdi = ReadCtxU64(contextRecord, CTX_RDI);
			var rsi = ReadCtxU64(contextRecord, CTX_RSI);
			var rdx = ReadCtxU64(contextRecord, 136);
			var rcx = ReadCtxU64(contextRecord, CTX_RCX);
			var r8 = ReadCtxU64(contextRecord, 184);
			var r9 = ReadCtxU64(contextRecord, 192);
			var r12 = ReadCtxU64(contextRecord, 216);
			var r13 = ReadCtxU64(contextRecord, 224);
			var r14 = ReadCtxU64(contextRecord, 232);
			var r15 = ReadCtxU64(contextRecord, 240);
			Console.Error.WriteLine(
				$"[LOADER][WARN] guest-bp 0x{rip:X16} hit#{hit + 1}: rdi=0x{rdi:X16} rsi=0x{rsi:X16} rdx=0x{rdx:X16} rcx=0x{rcx:X16} r8=0x{r8:X16} r9=0x{r9:X16} r12=0x{r12:X16} r13=0x{r13:X16} r14=0x{r14:X16} r15=0x{r15:X16}");
			Console.Error.Flush();
		}

		// Restore the original byte, single-step over it, then re-patch (in the step handler).
		uint oldProtect;
		if (VirtualProtect((void*)rip, 1, 0x40u, &oldProtect))
		{
			*(byte*)rip = original;
			uint restore;
			VirtualProtect((void*)rip, 1, oldProtect, &restore);
		}

		_guestBreakpointStepAddress = rip;
		var eflags = (uint)Marshal.ReadInt32((nint)contextRecord + CtxEFlagsOffset);
		Marshal.WriteInt32((nint)contextRecord + CtxEFlagsOffset, (int)(eflags | EFlagsTrapFlag));
		return true;
	}

	private unsafe bool TryCompleteGuestBreakpointStep(void* contextRecord)
	{
		var address = _guestBreakpointStepAddress;
		if (address == 0)
		{
			return false;
		}

		_guestBreakpointStepAddress = 0;
		lock (_guestBreakpointGate)
		{
			if (_guestBreakpointOriginal.ContainsKey(address))
			{
				uint oldProtect;
				if (VirtualProtect((void*)address, 1, 0x40u, &oldProtect))
				{
					*(byte*)address = 0xCC;
					uint restore;
					VirtualProtect((void*)address, 1, oldProtect, &restore);
				}
			}
		}

		var eflags = (uint)Marshal.ReadInt32((nint)contextRecord + CtxEFlagsOffset);
		Marshal.WriteInt32((nint)contextRecord + CtxEFlagsOffset, (int)(eflags & ~EFlagsTrapFlag));
		return true;
	}

	private static void StartWatchWritesArmingThread()
	{
		if (_watchWrites.Address == 0)
		{
			return;
		}

		Console.Error.WriteLine(
			$"[LOADER][INFO] watch-writes requested @0x{_watchWrites.Address:X16} len=0x{_watchWrites.Length:X}; arming once the page is committed.");
		var armingThread = new Thread(static () =>
		{
			while (Volatile.Read(ref _watchWritesArmed) == 0)
			{
				TryArmWatchWrites();
				Thread.Sleep(20);
			}
		})
		{
			IsBackground = true,
			Name = "sharpemu-watch-writes",
		};
		armingThread.Start();
	}

	private static unsafe void TryArmWatchWrites()
	{
		// The first page must be committed before we protect anything; later pages of a multi-page
		// range are protected best-effort (a range can straddle a not-yet-committed tail).
		var firstPage = WatchWritesFirstPage;
		if (VirtualQuery((void*)firstPage, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
			mbi.State != MEM_COMMIT ||
			(mbi.Protect & (0x04u | 0x08u | 0x40u | 0x80u)) == 0)
		{
			return;
		}

		var protectedPages = 0;
		for (var page = firstPage; page <= WatchWritesLastPage; page += 0x1000)
		{
			if (VirtualQuery((void*)page, out var pageInfo, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) != 0 &&
				pageInfo.State == MEM_COMMIT &&
				(pageInfo.Protect & (0x04u | 0x08u | 0x40u | 0x80u)) != 0)
			{
				uint oldProtect;
				if (VirtualProtect((void*)page, 0x1000, WatchPageReadOnly, &oldProtect))
				{
					protectedPages++;
				}
			}
		}

		if (protectedPages > 0)
		{
			Volatile.Write(ref _watchWritesArmed, 1);
			Console.Error.WriteLine(
				$"[LOADER][WARN] watch-writes ARMED on {protectedPages} page(s) 0x{firstPage:X16}..0x{WatchWritesLastPage:X16}" +
				(_watchWritesOnlyFF ? " (0xFFFFFFFF filter)." : "."));
			Console.Error.Flush();
		}
	}

	private static bool IsWatchedPage(ulong page) => page >= WatchWritesFirstPage && page <= WatchWritesLastPage;

	private unsafe bool TryHandleWatchedWriteFault(EXCEPTION_RECORD* exceptionRecord, void* contextRecord, ulong rip)
	{
		if (Volatile.Read(ref _watchWritesArmed) != 1 || exceptionRecord->NumberParameters < 2)
		{
			return false;
		}

		ulong accessType = *exceptionRecord->ExceptionInformation;
		ulong target = exceptionRecord->ExceptionInformation[1];
		var page = target & ~0xFFFUL;
		if (accessType != 1 || !IsWatchedPage(page))
		{
			return false;
		}

		uint oldProtect;
		VirtualProtect((void*)page, 0x1000, WatchPageReadWrite, &oldProtect);
		_watchWritesStepPending = true;
		_watchWritesStepTarget = target;
		_watchWritesStepRip = rip;
		_watchWritesStepPage = page;
		var eflags = (uint)Marshal.ReadInt32((nint)contextRecord + CtxEFlagsOffset);
		Marshal.WriteInt32((nint)contextRecord + CtxEFlagsOffset, (int)(eflags | EFlagsTrapFlag));
		return true;
	}

	private unsafe bool TryCompleteWatchedWriteStep(void* contextRecord)
	{
		if (!_watchWritesStepPending)
		{
			return false;
		}

		_watchWritesStepPending = false;
		var target = _watchWritesStepTarget;
		var writerRip = _watchWritesStepRip;
		var faultedPage = _watchWritesStepPage;
		var inRange = target >= _watchWrites.Address && target < _watchWrites.Address + _watchWrites.Length;

		ulong value = 0;
		try
		{
			value = (ulong)Marshal.ReadInt64((nint)(target & ~7UL));
		}
		catch
		{
		}

		// With the 0xFFFFFFFF filter, only a stored qword carrying an all-ones 32-bit half is of
		// interest (the (uint)-1 sentinel we are hunting); everything else is ignored so the whole
		// page can be watched without the event cap being consumed by ordinary traffic.
		var interesting = !_watchWritesOnlyFF ||
			(value & 0xFFFFFFFFUL) == 0xFFFFFFFFUL ||
			(value >> 32) == 0xFFFFFFFFUL;
		var counts = inRange && interesting;
		var eventIndex = counts ? Interlocked.Increment(ref _watchWritesEvents) : 0;
		var stepIndex = Interlocked.Increment(ref _watchWritesSteps);
		if (stepIndex >= WatchWritesMaxSteps)
		{
			if (Interlocked.Exchange(ref _watchWritesArmed, 2) == 1)
			{
				Console.Error.WriteLine($"[LOADER][WARN] watch-writes disarmed (step cap {WatchWritesMaxSteps} reached); pages left writable.");
				Console.Error.Flush();
			}

			var restoreEflags = (uint)Marshal.ReadInt32((nint)contextRecord + CtxEFlagsOffset);
			Marshal.WriteInt32((nint)contextRecord + CtxEFlagsOffset, (int)(restoreEflags & ~EFlagsTrapFlag));
			return true;
		}

		if (counts || (!_watchWritesOnlyFF && !inRange && stepIndex <= 6))
		{
			var instructionText = "?";
			if (TryReadHostBytes(writerRip, 15, out var codeBytes) &&
				IcedDecoder.TryDecode(writerRip, codeBytes, out var instruction))
			{
				instructionText = instruction.Text;
			}

			// For a `rep movs` fill the writer's rdi/rsi/rcx are the live dest/source/remaining-count,
			// so an in-range hit tells us whether (and with what size/source) the copy that should fill
			// the stream buffer ever reaches this slot — the datum that distinguishes a truncated fill
			// from a stale-pool over-run.
			var rcxReg = (ulong)Marshal.ReadInt64((nint)contextRecord + 0x80);
			var rdxReg = (ulong)Marshal.ReadInt64((nint)contextRecord + 0x88);
			var rsiReg = (ulong)Marshal.ReadInt64((nint)contextRecord + 0xA8);
			var rdiReg = (ulong)Marshal.ReadInt64((nint)contextRecord + 0xB0);
			var raxReg = (ulong)Marshal.ReadInt64((nint)contextRecord + 0x78);

			Console.Error.WriteLine(
				$"[LOADER][WARN] watch-writes {(inRange ? $"IN-RANGE hit#{eventIndex}" : $"page-step#{stepIndex}")}: " +
				$"writer=0x{writerRip:X16} '{instructionText}' target=0x{target:X16} qword=0x{value:X16} " +
				$"rax=0x{raxReg:X16} rcx=0x{rcxReg:X16} rdx=0x{rdxReg:X16} rsi=0x{rsiReg:X16} rdi=0x{rdiReg:X16}");
			Console.Error.Flush();
		}

		var eflags = (uint)Marshal.ReadInt32((nint)contextRecord + CtxEFlagsOffset);
		Marshal.WriteInt32((nint)contextRecord + CtxEFlagsOffset, (int)(eflags & ~EFlagsTrapFlag));

		if (eventIndex >= WatchWritesMaxEvents)
		{
			if (Interlocked.Exchange(ref _watchWritesArmed, 2) == 1)
			{
				Console.Error.WriteLine("[LOADER][WARN] watch-writes disarmed (event cap reached); pages left writable.");
				Console.Error.Flush();
			}

			return true;
		}

		uint reArmProtect;
		VirtualProtect((void*)faultedPage, 0x1000, WatchPageReadOnly, &reArmProtect);
		return true;
	}

	private static readonly bool _skipGuestDivideByZero =
		string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_SKIP_DIV_ZERO"), "1", StringComparison.Ordinal);

	private readonly Dictionary<ulong, int> _skippedDivideSites = new();

	// Emulates a guest integer division whose divisor is zero (e.g. FMOD's audio ring length when
	// the audio configuration deserializes empty) as quotient 0 / remainder 0 and resumes at the
	// next instruction, instead of letting the exception terminate the process. The affected
	// subsystem degrades (silent audio) but boot can proceed to the next real wall. Opt-in via
	// SHARPEMU_SKIP_DIV_ZERO=1 and restricted to guest code addresses.
	private unsafe bool TryHandleGuestDivideByZero(void* contextRecord, ulong rip)
	{
		if (!_skipGuestDivideByZero || rip < 0x0000000800000000UL || rip >= 0x0000000810000000UL)
		{
			return false;
		}

		try
		{
			var bytes = new ReadOnlySpan<byte>((void*)rip, 15);
			if (!IcedDecoder.TryDecode(rip, bytes, out var instruction) ||
				(!string.Equals(instruction.Mnemonic, "Div", StringComparison.OrdinalIgnoreCase) &&
				 !string.Equals(instruction.Mnemonic, "Idiv", StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}

			WriteCtxU64(contextRecord, CTX_RAX, 0);
			WriteCtxU64(contextRecord, 136, 0); // RDX (remainder)
			WriteCtxU64(contextRecord, 248, rip + (ulong)instruction.Length);

			int count;
			lock (_skippedDivideSites)
			{
				_skippedDivideSites.TryGetValue(rip, out count);
				_skippedDivideSites[rip] = count + 1;
			}

			if (count < 4)
			{
				Console.Error.WriteLine(
					$"[LOADER][WARN] Guest divide-by-zero at 0x{rip:X16} ({instruction.Text}); " +
					"emulated as 0 and skipped (SHARPEMU_SKIP_DIV_ZERO).");
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	private static readonly bool _skipRunawayCopy =
		string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_SKIP_BIG_COPY"), "1", StringComparison.Ordinal);

	// Maximum plausible single memory-copy: anything larger is treated as a corrupted length rather
	// than a real transfer. GRIS's serializer occasionally reads a (uint)-1 string length from a
	// derived buffer; the resulting `rep movs` walks off mapped memory. This is a triage aid, not a
	// correctness fix — it lets boot proceed past the faulting copy to reveal any later walls.
	private const ulong RunawayCopyThreshold = 0x1000_0000UL; // 256 MiB

	private readonly Dictionary<ulong, int> _skippedRunawayCopySites = new();

	// Aborts a `rep movs`/`rep stos` whose remaining count (RCX) is absurdly large by zeroing RCX and
	// resuming, so the string instruction retires as a no-op and control falls through. Opt-in via
	// SHARPEMU_SKIP_BIG_COPY=1; only fires on a fault whose faulting instruction is actually a REP
	// string op with a count over the runaway threshold, so it never masks a normal bounded copy.
	private unsafe bool TryRecoverGuestRunawayCopy(EXCEPTION_RECORD* exceptionRecord, void* contextRecord, ulong rip)
	{
		if (!_skipRunawayCopy || rip < 0x0000000800000000UL || rip >= 0x0000000810000000UL)
		{
			return false;
		}

		ulong count = ReadCtxU64(contextRecord, CTX_RCX);
		if (count < RunawayCopyThreshold)
		{
			return false;
		}

		try
		{
			// Skip any REP prefixes (F2/F3) and the address-size/REX bytes to find the string opcode.
			var code = new ReadOnlySpan<byte>((void*)rip, 4);
			var i = 0;
			var sawRep = false;
			while (i < code.Length && (code[i] == 0xF3 || code[i] == 0xF2 || code[i] == 0x67))
			{
				sawRep |= code[i] != 0x67;
				i++;
			}

			if (i < code.Length && code[i] >= 0x40 && code[i] <= 0x4F) // optional REX
			{
				i++;
			}

			var opcode = i < code.Length ? code[i] : (byte)0;
			var isStringOp = opcode is 0xA4 or 0xA5 or 0xAA or 0xAB; // movs/stos (b/wd)
			if (!sawRep || !isStringOp)
			{
				return false;
			}

			WriteCtxU64(contextRecord, CTX_RCX, 0);

			int seen;
			lock (_skippedRunawayCopySites)
			{
				_skippedRunawayCopySites.TryGetValue(rip, out seen);
				_skippedRunawayCopySites[rip] = seen + 1;
			}

			if (seen < 4)
			{
				ulong faultAddress = exceptionRecord->NumberParameters >= 2 ? exceptionRecord->ExceptionInformation[1] : 0;
				Console.Error.WriteLine(
					$"[LOADER][WARN] Runaway rep-string at 0x{rip:X16} count=0x{count:X16} fault=0x{faultAddress:X16}; " +
					"aborted (RCX=0) via SHARPEMU_SKIP_BIG_COPY.");
				Console.Error.Flush();
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	// Steps over a guest store to an inaccessible address (garbage index / OOB write from an
	// incomplete-VM value) instead of letting the uncatchable AV terminate the process. Only engages
	// when SHARPEMU_SKIP_GARBAGE_STORES=1, only for write faults, and only when the target is genuinely
	// not a committed writable page (so it never masks a fault on real, mapped guest memory).
	private unsafe bool TryRecoverGuestGarbageStore(EXCEPTION_RECORD* exceptionRecord, void* contextRecord, ulong rip)
	{
		if (!_skipGarbageStores || exceptionRecord->NumberParameters < 2)
		{
			return false;
		}

		ulong accessType = *exceptionRecord->ExceptionInformation;
		if (accessType != 0 && accessType != 1)
		{
			return false;
		}

		ulong target = exceptionRecord->ExceptionInformation[1];

		// A committed page already permitting this access would not have faulted, so if we see one the
		// fault is something else entirely — leave it for the fatal path rather than silently skipping.
		var accessibleMask = accessType == 1 ? 0xCCu : 0xEEu;
		if (VirtualQuery((void*)target, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) != 0 &&
			mbi.State == MEM_COMMIT &&
			(mbi.Protect & accessibleMask) != 0 &&
			(mbi.Protect & 0x100u) == 0)
		{
			return false;
		}

		byte[] instructionBytes = new byte[15];
		try
		{
			Marshal.Copy((nint)rip, instructionBytes, 0, instructionBytes.Length);
		}
		catch
		{
			return false;
		}

		if (!IcedDecoder.TryDecode(rip, instructionBytes, out var instruction) || instruction.Length <= 0)
		{
			return false;
		}

		// A read fault can only be dropped safely for a bulk string op (rep movs/stos/...), where
		// abandoning the whole transfer is well-defined. Skipping a scalar load would leave its
		// destination register holding stale garbage, so those stay fatal.
		if (accessType == 0 && !IsRepStringOp(instructionBytes))
		{
			return false;
		}

		long count = Interlocked.Increment(ref _garbageStoreSkips);
		if (count <= 32 || count % 4096 == 0)
		{
			Console.Error.WriteLine(
				$"[LOADER][WARN] Skipped garbage guest {(accessType == 1 ? "store" : "read")} #{count} at rip=0x{rip:X16} " +
				$"target=0x{target:X16} len={instruction.Length} ({instruction.Text}).");
			Console.Error.Flush();
		}

		WriteCtxU64(contextRecord, CTX_RIP, rip + (ulong)instruction.Length);
		return true;
	}

	// True for a REP-prefixed x86 string instruction (movs/stos/cmps/lods/scas), after an optional REX.
	private static bool IsRepStringOp(byte[] bytes)
	{
		var i = 0;
		if (i >= bytes.Length || (bytes[i] != 0xF3 && bytes[i] != 0xF2))
		{
			return false;
		}

		i++;
		if (i < bytes.Length && (bytes[i] & 0xF0) == 0x40)
		{
			i++;
		}

		if (i >= bytes.Length)
		{
			return false;
		}

		return bytes[i] is 0xA4 or 0xA5 or 0xA6 or 0xA7 or 0xAA or 0xAB or 0xAC or 0xAD or 0xAE or 0xAF;
	}

	private static bool IsBenignHostDebugException(uint exceptionCode)
	{
		return exceptionCode is DBG_PRINTEXCEPTION_C or DBG_PRINTEXCEPTION_WIDE_C or MS_VC_THREADNAME_EXCEPTION;
	}

	private unsafe static void LogNestedVectoredException(void* exceptionInfo)
	{
		int count = Interlocked.Increment(ref _nestedVehTraceCount);
		if (count > 16 && count % 128 != 0)
		{
			return;
		}

		try
		{
			EXCEPTION_POINTERS* pointers = (EXCEPTION_POINTERS*)exceptionInfo;
			EXCEPTION_RECORD* record = pointers->ExceptionRecord;
			void* contextRecord = pointers->ContextRecord;
			ulong rip = contextRecord != null ? ReadCtxU64(contextRecord, 248) : 0;
			ulong rsp = contextRecord != null ? ReadCtxU64(contextRecord, 152) : 0;
			ulong accessType = record->NumberParameters >= 1 ? *record->ExceptionInformation : 0;
			ulong target = record->NumberParameters >= 2 ? record->ExceptionInformation[1] : 0;
			Console.Error.WriteLine(
				$"[LOADER][TRACE] Nested VEH exception#{count}: code=0x{record->ExceptionCode:X8} ex=0x{(ulong)record->ExceptionAddress:X16} rip=0x{rip:X16} rsp=0x{rsp:X16} type={accessType} target=0x{target:X16}; passing through.");
		}
		catch
		{
			Console.Error.WriteLine($"[LOADER][TRACE] Nested VEH exception#{count}; passing through.");
		}
	}

	private unsafe void LogAccessViolationTrace(ulong exceptionAddress, EXCEPTION_RECORD* exceptionRecord)
	{
		ulong accessType = exceptionRecord->NumberParameters >= 1 ? (*exceptionRecord->ExceptionInformation) : 0;
		ulong target = exceptionRecord->NumberParameters >= 2 ? exceptionRecord->ExceptionInformation[1] : 0;
		if (_lastAvTraceRip == exceptionAddress && _lastAvTraceType == accessType && _lastAvTraceTarget == target)
		{
			_lastAvTraceRepeatCount++;
			if (_lastAvTraceRepeatCount > 4 && _lastAvTraceRepeatCount % 128 != 0)
			{
				return;
			}
			Console.Error.WriteLine($"[LOADER][TRACE] VEH_AV repeat#{_lastAvTraceRepeatCount} at 0x{exceptionAddress:X16} type={accessType} target=0x{target:X16}");
			Console.Error.Flush();
			return;
		}

		_lastAvTraceRip = exceptionAddress;
		_lastAvTraceType = accessType;
		_lastAvTraceTarget = target;
		_lastAvTraceRepeatCount = 1;
		Console.Error.WriteLine($"[LOADER][TRACE] VEH_AV first-chance at 0x{exceptionAddress:X16} type={accessType} target=0x{target:X16}");
		Console.Error.Flush();
	}

	private void DumpGuestInstructionStream(string name, ulong startRip, int maxInstructions)
	{
		if (_cpuContext == null || startRip < 0x10000 || maxInstructions <= 0)
		{
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO]   {name} disasm @0x{startRip:X16}:");
		ulong rip = startRip;
		for (int i = 0; i < maxInstructions; i++)
		{
			if (!IcedDecoder.TryReadGuestBytes(_cpuContext.Memory, rip, maxLen: 15, out var bytes) ||
				!IcedDecoder.TryDecode(rip, bytes, out var instruction))
			{
				Console.Error.WriteLine($"[LOADER][INFO]     0x{rip:X16}: <decode-failed>");
				break;
			}

			Console.Error.WriteLine(
				$"[LOADER][INFO]     0x{instruction.Rip:X16}: {instruction.Text} bytes={IcedDecoder.FormatBytes(instruction.Bytes)}");
			rip += (ulong)instruction.Length;
		}
	}

	private void DumpGuestDisasmDiagnostics(ulong rip, ulong rbp)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_LOG_DISASM"), "1", StringComparison.Ordinal))
		{
			return;
		}

		if (rip >= 0x20)
		{
			DumpGuestInstructionStream("fault-prelude", rip - 0x20, 24);
		}

		try
		{
			ulong frame = rbp;
			for (int i = 0; i < 3; i++)
			{
				if (frame < 140733193388032L || frame > 140737488355327L)
				{
					break;
				}

				ulong ret = (ulong)Marshal.ReadInt64((nint)(frame + 8));
				if (ret >= 0x40)
				{
					DumpGuestInstructionStream($"frame#{i}-ret-prelude", ret - 0x40, 24);
				}

				ulong next = (ulong)Marshal.ReadInt64((nint)frame);
				if (next <= frame)
				{
					break;
				}

				frame = next;
			}
		}
		catch
		{
			Console.Error.WriteLine("[LOADER][WARNING]   Could not dump disasm diagnostics.");
		}

		var extraAddresses = Environment.GetEnvironmentVariable("SHARPEMU_LOG_DISASM_ADDRS");
		if (string.IsNullOrWhiteSpace(extraAddresses))
		{
			return;
		}

		foreach (var token in extraAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var normalized = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? token[2..]
				: token;
			if (!ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) || address < 0x20)
			{
				continue;
			}

			DumpGuestInstructionStream($"extra-0x{address:X16}", address, 48);
		}
	}

	// Reads host-process memory only after confirming every touched page is committed and readable,
	// so crash-report dumps can never raise a nested exception inside the vectored handler.
	private static unsafe bool TryReadHostBytes(ulong address, int length, out byte[] bytes)
	{
		bytes = Array.Empty<byte>();
		if (address == 0 || length <= 0)
		{
			return false;
		}

		var page = address & ~0xFFFUL;
		var lastPage = (address + (ulong)length - 1) & ~0xFFFUL;
		for (var probe = page; probe <= lastPage; probe += 0x1000)
		{
			if (VirtualQuery((void*)probe, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				mbi.State != MEM_COMMIT ||
				!IsReadableProtection(mbi.Protect))
			{
				return false;
			}
		}

		bytes = new byte[length];
		Marshal.Copy((nint)address, bytes, 0, length);
		return true;
	}

	// SHARPEMU_SCAN_BYTES=<hex>[,<hex>...]: scans executable guest code for raw byte patterns at
	// crash time (e.g. "893568350000" finds every `mov [reg+0x3568], r32`) so the instruction that
	// should have initialised a field can be located without external disassembly of the SELF.
	private unsafe void DumpGuestByteScanDiagnostics()
	{
		var rawPatterns = Environment.GetEnvironmentVariable("SHARPEMU_SCAN_BYTES");
		if (string.IsNullOrWhiteSpace(rawPatterns))
		{
			return;
		}

		const ulong scanBase = 0x0000000800000000UL;
		const ulong scanEnd = 0x0000000810000000UL;
		const int maxHitsPerPattern = 48;

		foreach (var token in rawPatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var normalized = token.Replace(" ", string.Empty);
			if (normalized.Length < 4 || normalized.Length % 2 != 0)
			{
				continue;
			}

			var pattern = new byte[normalized.Length / 2];
			var valid = true;
			for (var i = 0; i < pattern.Length; i++)
			{
				if (!byte.TryParse(normalized.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pattern[i]))
				{
					valid = false;
					break;
				}
			}

			if (!valid)
			{
				continue;
			}

			var hits = 0;
			ulong address = scanBase;
			while (address < scanEnd && hits < maxHitsPerPattern)
			{
				if (VirtualQuery((void*)address, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0)
				{
					break;
				}

				ulong regionBase = mbi.BaseAddress;
				ulong regionEnd = regionBase + mbi.RegionSize;
				if (regionEnd <= address)
				{
					break;
				}

				if (mbi.State == MEM_COMMIT && IsReadableProtection(mbi.Protect) && IsExecutableProtection(mbi.Protect))
				{
					var span = new ReadOnlySpan<byte>((void*)regionBase, checked((int)(regionEnd - regionBase)));
					var searchFrom = 0;
					while (hits < maxHitsPerPattern)
					{
						var index = span[searchFrom..].IndexOf(pattern);
						if (index < 0)
						{
							break;
						}

						var hitAddress = regionBase + (ulong)(searchFrom + index);
						Console.Error.WriteLine($"[LOADER][INFO]   byte-scan '{normalized}' hit @0x{hitAddress:X16}");
						hits++;
						searchFrom += index + 1;
					}
				}

				address = regionEnd;
			}

			if (hits == 0)
			{
				Console.Error.WriteLine($"[LOADER][INFO]   byte-scan '{normalized}': none");
			}
		}
	}

	private unsafe void DumpGuestReferenceDiagnostics()
	{
		var targetList = ParseDiagnosticAddresses(Environment.GetEnvironmentVariable("SHARPEMU_LOG_REFSCAN_ADDRS"));
		if (targetList.Count == 0 || _cpuContext == null)
		{
			return;
		}

		const ulong scanBase = 0x0000000800000000UL;
		const ulong scanEnd = 0x0000000810000000UL;
		const int maxHitsPerTarget = 24;

		Console.Error.WriteLine(
			$"[LOADER][INFO]   Ref scan targets: {string.Join(", ", targetList.ConvertAll(static addr => $"0x{addr:X16}"))}");

		var hitCounts = new Dictionary<ulong, int>(targetList.Count);
		for (var i = 0; i < targetList.Count; i++)
		{
			hitCounts[targetList[i]] = 0;
		}

		ulong address = scanBase;
		while (address < scanEnd)
		{
			if (VirtualQuery((void*)address, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0)
			{
				break;
			}

			ulong regionBase = mbi.BaseAddress;
			ulong regionEnd = regionBase + mbi.RegionSize;
			if (regionEnd <= address)
			{
				break;
			}

			if (mbi.State == MEM_COMMIT &&
				IsReadableProtection(mbi.Protect) &&
				IsExecutableProtection(mbi.Protect))
			{
				ScanExecutableRegionForTargetReferences(regionBase, regionEnd, targetList, hitCounts, maxHitsPerTarget);
			}

			var allTargetsSatisfied = true;
			for (var i = 0; i < targetList.Count; i++)
			{
				if (hitCounts[targetList[i]] < maxHitsPerTarget)
				{
					allTargetsSatisfied = false;
					break;
				}
			}

			if (allTargetsSatisfied)
			{
				break;
			}

			address = regionEnd;
		}

		for (var i = 0; i < targetList.Count; i++)
		{
			var target = targetList[i];
			if (!hitCounts.TryGetValue(target, out var count) || count == 0)
			{
				Console.Error.WriteLine($"[LOADER][INFO]   Ref scan 0x{target:X16}: none");
			}
		}
	}

	private void DumpGuestPointerWindowDiagnostics()
	{
		var targetList = ParseDiagnosticAddresses(Environment.GetEnvironmentVariable("SHARPEMU_LOG_POINTER_WINDOWS"));
		if (targetList.Count == 0)
		{
			return;
		}

		var windowSize = 0x80;
		var rawWindowSize = Environment.GetEnvironmentVariable("SHARPEMU_LOG_POINTER_WINDOW_SIZE");
		if (!string.IsNullOrWhiteSpace(rawWindowSize))
		{
			var normalized = rawWindowSize.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? rawWindowSize[2..]
				: rawWindowSize;
			if (int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedWindowSize) &&
				parsedWindowSize > 0)
			{
				windowSize = parsedWindowSize;
			}
		}

		foreach (var target in targetList)
		{
			DumpPointerWindow($"ptrwin-0x{target:X16}", target, windowSize);
		}
	}

	private void ScanExecutableRegionForTargetReferences(
		ulong regionBase,
		ulong regionEnd,
		IReadOnlyList<ulong> targets,
		IDictionary<ulong, int> hitCounts,
		int maxHitsPerTarget)
	{
		if (_cpuContext == null || regionEnd <= regionBase)
		{
			return;
		}

		ulong rip = regionBase;
		while (rip < regionEnd)
		{
			if (!IcedDecoder.TryReadGuestBytes(_cpuContext.Memory, rip, maxLen: 15, out var bytes) ||
				!IcedDecoder.TryDecode(rip, bytes, out var instruction) ||
				instruction.Length <= 0)
			{
				rip++;
				continue;
			}

			if (instruction.MemoryAddress is { } memoryAddress)
			{
				for (var i = 0; i < targets.Count; i++)
				{
					var target = targets[i];
					if (memoryAddress != target ||
						!hitCounts.TryGetValue(target, out var count) ||
						count >= maxHitsPerTarget)
					{
						continue;
					}

					hitCounts[target] = count + 1;
					Console.Error.WriteLine(
						$"[LOADER][INFO]   Ref scan hit target=0x{target:X16} rip=0x{instruction.Rip:X16} text={instruction.Text} bytes={IcedDecoder.FormatBytes(instruction.Bytes)}");
				}
			}

			rip += (ulong)instruction.Length;
		}
	}

	private static List<ulong> ParseDiagnosticAddresses(string? rawValue)
	{
		var result = new List<ulong>();
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return result;
		}

		foreach (var token in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var normalized = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? token[2..]
				: token;
			if (!ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address))
			{
				continue;
			}

			if (!result.Contains(address))
			{
				result.Add(address);
			}
		}

		return result;
	}

	private void DumpUnresolvedSentinelWindow(string name, ulong baseAddress, int size)
	{
		if (baseAddress < 0x10000 || size <= 0)
		{
			return;
		}

		ulong scanStart = baseAddress;
		ulong scanEnd = baseAddress + (ulong)size;
		List<ulong> hits = ScanSuspiciousResolverPointers(scanStart, scanEnd);
		if (hits.Count == 0)
		{
			Console.Error.WriteLine($"[LOADER][INFO]   {name} unresolved scan: none");
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO]   {name} unresolved scan hits: {hits.Count}");
		for (int i = 0; i < hits.Count && i < 8; i++)
		{
			ulong slotAddress = hits[i];
			if (TryReadQword(slotAddress, out var value))
			{
				Console.Error.WriteLine($"[LOADER][INFO]     hit#{i}: slot=0x{slotAddress:X16} value=0x{value:X16}");
			}
		}
	}

	private void DumpSentinelPatternWindow(string name, ulong baseAddress, int size)
	{
		if (_cpuContext == null || baseAddress < 0x10000 || size <= 0)
		{
			return;
		}

		byte[] buffer = new byte[size];
		if (!_cpuContext.Memory.TryRead(baseAddress, buffer))
		{
			Console.Error.WriteLine($"[LOADER][INFO]   {name} sentinel-pattern scan: unreadable");
			return;
		}

		List<string> hits = new();
		for (int offset = 0; offset + 2 <= buffer.Length; offset++)
		{
			if (BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, 2)) == 0xFFFE)
			{
				hits.Add($"+0x{offset:X}:u16");
			}

			if (offset + 4 <= buffer.Length &&
				BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4)) == 0xFFFFFFFEu)
			{
				hits.Add($"+0x{offset:X}:u32");
			}

			if (offset + 8 <= buffer.Length &&
				BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(offset, 8)) == 0xFFFFFFFFFFFFFFFEuL)
			{
				hits.Add($"+0x{offset:X}:u64");
			}
		}

		if (hits.Count == 0)
		{
			Console.Error.WriteLine($"[LOADER][INFO]   {name} sentinel-pattern scan: none");
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO]   {name} sentinel-pattern hits: {string.Join(", ", hits.GetRange(0, Math.Min(hits.Count, 12)))}");
	}

	private void DumpReturnTargetCandidates(ulong rsp)
	{
		if (rsp < 0x10000)
		{
			return;
		}

		ulong start = rsp >= 0x10 ? rsp - 0x10 : rsp;
		Console.Error.WriteLine($"[LOADER][INFO]   Return-target candidates near RSP=0x{rsp:X16}:");
		for (int offset = 0; offset <= 0x20; offset++)
		{
			ulong address = start + (ulong)offset;
			try
			{
				ulong value = (ulong)Marshal.ReadInt64((nint)address);
				Console.Error.WriteLine($"[LOADER][INFO]     [0x{address:X16}] -> 0x{value:X16}");
			}
			catch
			{
				Console.Error.WriteLine($"[LOADER][INFO]     [0x{address:X16}] -> <unreadable>");
				break;
			}
		}
	}

	private void DumpObjectFieldTargets(string name, ulong objectAddress, int[] offsets, int windowSize)
	{
		if (objectAddress < 0x10000 || offsets.Length == 0)
		{
			return;
		}

		foreach (int offset in offsets)
		{
			ulong slotAddress = objectAddress + (ulong)offset;
			if (!TryReadQword(slotAddress, out var target) || target < 0x10000)
			{
				continue;
			}

			Console.Error.WriteLine($"[LOADER][INFO]   {name}+0x{offset:X2} target = {FormatPointerWithNearestSymbol(target)}");
			DumpPointerWindow($"{name}+0x{offset:X2}", target, windowSize);
			DumpUnresolvedSentinelWindow($"{name}+0x{offset:X2}", target, 0x80);
		}
	}

	private void DumpSuspiciousGlobalSlots()
	{
		DumpAbsoluteSlot("callback_slot[0x80293BD08]", 0x000000080293BD08uL);
		DumpAbsoluteSlot("callback_arg[0x8030FBBE8]", 0x00000008030FBBE8uL);
		DumpAbsoluteSlot("tsc_freq_global[0x8030FD590]", 0x00000008030FD590uL);
		DumpAbsoluteSlot("tsc_base_global[0x8030FD598]", 0x00000008030FD598uL);
		DumpAbsoluteSlot("plt_got[0x8028F6100]", 0x00000008028F6100uL);
		DumpAbsoluteSlot("plt_got[0x8028F6158]", 0x00000008028F6158uL);
		DumpAbsoluteSlot("plt_got[0x8028F6160]", 0x00000008028F6160uL);
		DumpAbsoluteSlot("plt_got[0x8028F64C0]", 0x00000008028F64C0uL);
		DumpAbsoluteSlot("plt_got[0x8028F64C8]", 0x00000008028F64C8uL);
		DumpAbsoluteSlot("plt_got[0x8028F6590]", 0x00000008028F6590uL);
		DumpAbsoluteSlot("plt_got[0x8028F6708]", 0x00000008028F6708uL);
		DumpUnresolvedSentinelWindow("PLT-GOT", 0x00000008028F6100uL, 0x700);
	}

	private void DumpAbsoluteSlot(string name, ulong slotAddress)
	{
		if (!TryReadQword(slotAddress, out var value))
		{
			Console.Error.WriteLine($"[LOADER][INFO]   {name} @0x{slotAddress:X16} = <unreadable>");
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO]   {name} @0x{slotAddress:X16} = {FormatPointerWithNearestSymbol(value)}");
	}
	private void DumpPointerWindow(string name, ulong baseAddress, int size)
	{
		if (baseAddress < 0x10000 || size <= 0)
		{
			return;
		}

		Console.Error.WriteLine($"[LOADER][INFO]   {name} window @0x{baseAddress:X16}:");
		for (int offset = 0; offset < size; offset += 8)
		{
			ulong slotAddress = baseAddress + (ulong)offset;
			if (!TryReadQword(slotAddress, out var value))
			{
				Console.Error.WriteLine($"[LOADER][INFO]     +0x{offset:X2}: <unreadable>");
				break;
			}

			Console.Error.WriteLine($"[LOADER][INFO]     +0x{offset:X2}: {FormatPointerWithNearestSymbol(value)}");
		}
	}

	private unsafe bool TryReadQword(ulong address, out ulong value)
	{
		value = 0;
		if (address < 0x10000)
		{
			return false;
		}

		if (VirtualQuery((void*)address, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0)
		{
			return false;
		}

		ulong regionEnd = mbi.BaseAddress + mbi.RegionSize;
		if (mbi.State != MEM_COMMIT || !IsReadableProtection(mbi.Protect) || regionEnd <= address || address > regionEnd - 8)
		{
			return false;
		}

		try
		{
			value = (ulong)Marshal.ReadInt64((nint)address);
			return true;
		}
		catch
		{
			value = 0;
			return false;
		}
	}

	private string FormatPointerWithNearestSymbol(ulong value)
	{
		string text = $"0x{value:X16}";
		if (TryFormatNearestRuntimeSymbol(value, out string symbol))
		{
			text += $" [{symbol}]";
		}

		return text;
	}

	private void InitializeRuntimeSymbolIndex(IReadOnlyDictionary<string, ulong> runtimeSymbols)
	{
		_runtimeSymbolsByName.Clear();
		if (runtimeSymbols.Count == 0)
		{
			_runtimeSymbolsByAddress = Array.Empty<KeyValuePair<string, ulong>>();
			return;
		}

		List<KeyValuePair<string, ulong>> list = new(runtimeSymbols.Count);
		foreach (KeyValuePair<string, ulong> runtimeSymbol in runtimeSymbols)
		{
			if (runtimeSymbol.Value != 0L && !string.IsNullOrWhiteSpace(runtimeSymbol.Key))
			{
				list.Add(runtimeSymbol);
				_runtimeSymbolsByName[runtimeSymbol.Key] = runtimeSymbol.Value;
			}
		}

		list.Sort((a, b) => a.Value.CompareTo(b.Value));
		_runtimeSymbolsByAddress = list.ToArray();
	}

	private bool TryFormatNearestRuntimeSymbol(ulong address, out string text)
	{
		text = string.Empty;
		KeyValuePair<string, ulong>[] runtimeSymbolsByAddress = _runtimeSymbolsByAddress;
		if (runtimeSymbolsByAddress.Length == 0)
		{
			return false;
		}

		int low = 0;
		int high = runtimeSymbolsByAddress.Length - 1;
		int best = -1;
		while (low <= high)
		{
			int mid = low + ((high - low) >> 1);
			ulong value = runtimeSymbolsByAddress[mid].Value;
			if (value <= address)
			{
				best = mid;
				low = mid + 1;
			}
			else
			{
				high = mid - 1;
			}
		}

		if (best < 0)
		{
			return false;
		}

		KeyValuePair<string, ulong> symbol = runtimeSymbolsByAddress[best];
		ulong delta = address - symbol.Value;
		text = delta == 0
			? $"{symbol.Key} (0x{symbol.Value:X16})"
			: $"{symbol.Key}+0x{delta:X} (0x{symbol.Value:X16})";
		return true;
	}

	private unsafe bool TryHandleLazyCommittedPage(
		EXCEPTION_RECORD* exceptionRecord,
		void* contextRecord,
		ulong rip,
		ulong rsp)
	{
		if (exceptionRecord->NumberParameters < 2)
		{
			return false;
		}

		ulong accessType = *exceptionRecord->ExceptionInformation;
		ulong faultAddress = exceptionRecord->ExceptionInformation[1];
		if (accessType == 8 && faultAddress < 4294967296L)
		{
			return false;
		}
		if (faultAddress < 65536 || faultAddress >= 140737488355328L)
		{
			return false;
		}
		if (!IsGuestOwnedLazyCommitAddress(faultAddress, out var owner))
		{
			return false;
		}
		if (VirtualQuery((void*)faultAddress, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0)
		{
			return false;
		}

		ulong pageBase = faultAddress & 0xFFFFFFFFFFFFF000uL;
		uint commitProtect = ResolveLazyCommitProtection(accessType, mbi.AllocationProtect);
		int traceIndex = Interlocked.Increment(ref _lazyCommitTraceCount);
		bool traceLazyCommit = ShouldTraceLazyCommit(traceIndex);
		if (traceLazyCommit)
		{
			Console.Error.WriteLine($"[LOADER][TRACE] lazy-query#{traceIndex}: fault=0x{faultAddress:X16} owner={owner} rip=0x{rip:X16} rsp=0x{rsp:X16} state=0x{mbi.State:X08} base=0x{mbi.BaseAddress:X16} size=0x{mbi.RegionSize:X16} alloc=0x{mbi.AllocationProtect:X08} prot=0x{mbi.Protect:X08}");
			if (traceIndex <= 10)
			{
				var instructionBytes = new byte[15];
				Marshal.Copy((nint)rip, instructionBytes, 0, instructionBytes.Length);
				if (IcedDecoder.TryDecode(rip, instructionBytes, out var instruction))
				{
					Console.Error.WriteLine(
						$"[LOADER][TRACE] lazy-instruction#{traceIndex}: {instruction.Text} " +
						$"bytes={IcedDecoder.FormatBytes(instruction.Bytes)} " +
						$"rax=0x{ReadCtxU64(contextRecord, CTX_RAX):X16} " +
						$"rcx=0x{ReadCtxU64(contextRecord, CTX_RCX):X16} " +
						$"rsi=0x{ReadCtxU64(contextRecord, CTX_RSI):X16} " +
						$"rdi=0x{ReadCtxU64(contextRecord, CTX_RDI):X16}");
				}
			}
		}

		if (TrySkipSparseZeroFill(contextRecord, rip, faultAddress, owner, out var skippedBytes))
		{
			Console.Error.WriteLine(
				$"[LOADER][TRACE] lazy-zero-skip#{traceIndex}: " +
				$"addr=0x{faultAddress:X16} size=0x{skippedBytes:X16}");
			return true;
		}

		if (TrySkipSparseCopy(contextRecord, rip, faultAddress, out skippedBytes))
		{
			Console.Error.WriteLine(
				$"[LOADER][TRACE] lazy-copy-skip#{traceIndex}: " +
				$"fault=0x{faultAddress:X16} size=0x{skippedBytes:X16}");
			return true;
		}

		if (TrySkipSparseComparison(contextRecord, rip, faultAddress, out skippedBytes))
		{
			Console.Error.WriteLine(
				$"[LOADER][TRACE] lazy-compare-skip#{traceIndex}: " +
				$"fault=0x{faultAddress:X16} size=0x{skippedBytes:X16}");
			return true;
		}

		if (TrySkipSparseZeroRecordInitialization(
				contextRecord,
				rip,
				faultAddress,
				out skippedBytes))
		{
			Console.Error.WriteLine(
				$"[LOADER][TRACE] lazy-record-zero-skip#{traceIndex}: " +
				$"fault=0x{faultAddress:X16} size=0x{skippedBytes:X16}");
			return true;
		}

		if (mbi.State == 4096 && IsAccessCompatible(accessType, mbi.Protect))
		{
			if (traceLazyCommit)
			{
				Console.Error.WriteLine($"[LOADER][TRACE] lazy-commit-race#{traceIndex}: fault=0x{faultAddress:X16} protect=0x{mbi.Protect:X08}");
			}
			return true;
		}

		bool committed = false;
		ulong committedBase = 0;
		ulong committedSize = 0;

		if (mbi.State == 65536)
		{
			if (TryGetLazyCommitWindow(faultAddress, mbi.BaseAddress, mbi.RegionSize, out var windowBase, out var windowSize) &&
				TryReserveThenCommit(windowBase, windowSize, windowBase, windowSize, commitProtect))
			{
				committed = true;
				committedBase = windowBase;
				committedSize = windowSize;
			}
			else
			{
				ulong largeBase = faultAddress & 0xFFFFFFFFFFE00000uL;
				if (TryReserveThenCommit(largeBase, 2097152uL, largeBase, 2097152uL, commitProtect))
				{
					committed = true;
					committedBase = largeBase;
					committedSize = 2097152uL;
				}
			}

			if (!committed)
			{
				ulong region64kBase = faultAddress & 0xFFFFFFFFFFFF0000uL;
				if (TryReserveThenCommit(region64kBase, 65536uL, region64kBase, 65536uL, commitProtect))
				{
					committed = true;
					committedBase = region64kBase;
					committedSize = 65536uL;
				}
				else if (TryReserveThenCommit(pageBase, 4096uL, pageBase, 4096uL, commitProtect))
				{
					committed = true;
					committedBase = pageBase;
					committedSize = 4096uL;
				}
			}

			if (!committed)
			{
				return false;
			}

			TryCommitRange(pageBase + 4096, 4096uL, commitProtect);
			if (traceLazyCommit)
			{
				Console.Error.WriteLine($"[LOADER][TRACE] lazy-reserve-commit#{traceIndex}: addr=0x{committedBase:X16} size=0x{committedSize:X16} access={accessType} protect=0x{commitProtect:X8}");
			}
			return true;
		}

		if (mbi.State != 8192)
		{
			return false;
		}

		if (TryGetLazyCommitWindow(faultAddress, mbi.BaseAddress, mbi.RegionSize, out var commitWindowBase, out var commitWindowSize) &&
			TryCommitRange(commitWindowBase, commitWindowSize, commitProtect))
		{
			committed = true;
			committedBase = commitWindowBase;
			committedSize = commitWindowSize;
		}
		else
		{
			ulong largeCommitBase = faultAddress & 0xFFFFFFFFFFE00000uL;
			if (TryCommitRange(largeCommitBase, 2097152uL, commitProtect))
			{
				committed = true;
				committedBase = largeCommitBase;
				committedSize = 2097152uL;
			}
		}

		if (!committed)
		{
			ulong region64kBase = faultAddress & 0xFFFFFFFFFFFF0000uL;
			if (TryCommitRange(region64kBase, 65536uL, commitProtect))
			{
				committed = true;
				committedBase = region64kBase;
				committedSize = 65536uL;
			}
			else if (TryCommitRange(pageBase, 8192uL, commitProtect))
			{
				committed = true;
				committedBase = pageBase;
				committedSize = 8192uL;
			}
			else if (TryCommitRange(pageBase, 4096uL, commitProtect))
			{
				committed = true;
				committedBase = pageBase;
				committedSize = 4096uL;
			}
		}

		if (!committed)
		{
			return false;
		}

		TryCommitRange(pageBase + 4096, 4096uL, commitProtect);
		if (traceLazyCommit)
		{
			Console.Error.WriteLine($"[LOADER][TRACE] lazy-commit#{traceIndex}: addr=0x{committedBase:X16} size=0x{committedSize:X16} access={accessType} protect=0x{commitProtect:X8}");
		}
		return true;

		static bool TryGetLazyCommitWindow(ulong fault, ulong regionBase, ulong regionSize, out ulong baseAddress, out ulong length)
		{
			baseAddress = 0;
			length = 0;
			if (regionSize == 0 || ulong.MaxValue - regionBase < regionSize)
			{
				return false;
			}

			ulong regionEnd = regionBase + regionSize;
			ulong windowBase = fault & ~(LazyCommitWindowBytes - 1);
			if (windowBase < regionBase)
			{
				windowBase = regionBase;
			}

			if (windowBase >= regionEnd)
			{
				return false;
			}

			ulong windowEnd = Math.Min(regionEnd, windowBase + LazyCommitWindowBytes);
			ulong windowSize = windowEnd - windowBase;
			windowSize &= 0xFFFFFFFFFFFFF000uL;
			if (windowSize == 0)
			{
				return false;
			}

			baseAddress = windowBase;
			length = windowSize;
			return true;
		}

		static unsafe bool TryCommitRange(ulong baseAddress, ulong length, uint protection)
		{
			if (length == 0)
			{
				return false;
			}
			return VirtualAlloc((void*)baseAddress, (nuint)length, 4096u, protection) != null;
		}

		static unsafe bool TryReserveRange(ulong baseAddress, ulong length)
		{
			if (length == 0)
			{
				return false;
			}
			return VirtualAlloc((void*)baseAddress, (nuint)length, 8192u, 4u) != null;
		}

		static bool TryReserveThenCommit(ulong reserveAddress, ulong reserveSize, ulong commitAddress, ulong commitSize, uint protection)
		{
			if (!TryReserveRange(reserveAddress, reserveSize))
			{
				return false;
			}
			return TryCommitRange(commitAddress, commitSize, protection);
		}

		static bool IsAccessCompatible(ulong accessType, uint protection)
		{
			const uint pageNoAccess = 0x01;
			const uint pageReadOnly = 0x02;
			const uint pageReadWrite = 0x04;
			const uint pageWriteCopy = 0x08;
			const uint pageExecute = 0x10;
			const uint pageExecuteRead = 0x20;
			const uint pageExecuteReadWrite = 0x40;
			const uint pageExecuteWriteCopy = 0x80;
			const uint pageGuard = 0x100;
			const uint accessMask = 0xFF;

			if ((protection & pageGuard) != 0)
			{
				return false;
			}

			uint access = protection & accessMask;
			if (access == pageNoAccess)
			{
				return false;
			}

			return accessType switch
			{
				0 => access is pageReadOnly or pageReadWrite or pageWriteCopy or pageExecuteRead or pageExecuteReadWrite or pageExecuteWriteCopy,
				1 => access is pageReadWrite or pageWriteCopy or pageExecuteReadWrite or pageExecuteWriteCopy,
				8 => access is pageExecute or pageExecuteRead or pageExecuteReadWrite or pageExecuteWriteCopy,
				_ => false
			};
		}
	}

	private unsafe bool TrySkipSparseZeroFill(
		void* contextRecord,
		ulong rip,
		ulong faultAddress,
		string owner,
		out ulong skippedBytes)
	{
		skippedBytes = 0;
		if (*(byte*)rip != 0xF3 ||
			*((byte*)rip + 1) != 0xAA ||
			(ReadCtxU32(contextRecord, CTX_EFLAGS) & 0x400u) != 0)
		{
			return false;
		}

		var destination = ReadCtxU64(contextRecord, CTX_RDI);
		var count = ReadCtxU64(contextRecord, CTX_RCX);
		var value = (byte)ReadCtxU64(contextRecord, CTX_RAX);
		if (value != 0 ||
			count == 0 ||
			destination != faultAddress ||
			!TryAddRange(destination, count, out var endAddress) ||
			!IsGuestOwnedLazyCommitAddress(endAddress - 1, out var endOwner) ||
			!string.Equals(owner, endOwner, StringComparison.Ordinal) ||
			!TryResetSparseZeroRange(destination, endAddress))
		{
			return false;
		}

		WriteCtxU64(contextRecord, CTX_RDI, endAddress);
		WriteCtxU64(contextRecord, CTX_RCX, 0);
		WriteCtxU64(contextRecord, CTX_RIP, rip + 2);
		skippedBytes = count;
		return true;
	}

	private unsafe bool TrySkipSparseCopy(
		void* contextRecord,
		ulong rip,
		ulong faultAddress,
		out ulong skippedBytes)
	{
		skippedBytes = 0;
		if (*(byte*)rip != 0xF3 ||
			*((byte*)rip + 1) != 0xA4 ||
			(ReadCtxU32(contextRecord, CTX_EFLAGS) & 0x400u) != 0)
		{
			return false;
		}

		var source = ReadCtxU64(contextRecord, CTX_RSI);
		var destination = ReadCtxU64(contextRecord, CTX_RDI);
		var count = ReadCtxU64(contextRecord, CTX_RCX);
		if (count == 0 ||
			(faultAddress != source && faultAddress != destination) ||
			!TryAddRange(source, count, out var sourceEnd) ||
			!TryAddRange(destination, count, out var destinationEnd) ||
			!IsGuestOwnedLazyCommitAddress(source, out _) ||
			!IsGuestOwnedLazyCommitAddress(sourceEnd - 1, out _) ||
			!IsGuestOwnedLazyCommitAddress(destination, out _) ||
			!IsGuestOwnedLazyCommitAddress(destinationEnd - 1, out _) ||
			!TryCopySparseRange(source, destination, count))
		{
			return false;
		}

		WriteCtxU64(contextRecord, CTX_RSI, sourceEnd);
		WriteCtxU64(contextRecord, CTX_RDI, destinationEnd);
		WriteCtxU64(contextRecord, CTX_RCX, 0);
		WriteCtxU64(contextRecord, CTX_RIP, rip + 2);
		skippedBytes = count;
		return true;
	}

	private unsafe bool TrySkipSparseComparison(
		void* contextRecord,
		ulong rip,
		ulong faultAddress,
		out ulong skippedBytes)
	{
		skippedBytes = 0;
		ReadOnlySpan<byte> loopPattern =
		[
			0x41, 0x0F, 0xB6, 0x5C, 0x05, 0x00,
			0x41, 0x0F, 0xB6, 0x14, 0x02,
			0x38, 0xD3,
			0x75, 0x11,
			0x48, 0xFF, 0xC0,
			0x48, 0x39, 0xC7,
			0x75, 0xE9,
		];

		ulong loopStart;
		if (rip >= 6 &&
			*(byte*)rip == 0x41 &&
			*((byte*)rip + 1) == 0x0F &&
			*((byte*)rip + 3) == 0x5C)
		{
			loopStart = rip;
		}
		else if (rip >= 6)
		{
			loopStart = rip - 6;
		}
		else
		{
			return false;
		}

		if (!new ReadOnlySpan<byte>((void*)loopStart, loopPattern.Length).SequenceEqual(loopPattern))
		{
			return false;
		}

		var offset = ReadCtxU64(contextRecord, CTX_RAX);
		var length = ReadCtxU64(contextRecord, CTX_RDI);
		var left = ReadCtxU64(contextRecord, CTX_R13);
		var right = ReadCtxU64(contextRecord, CTX_R10);
		if (offset >= length ||
			!TryAddRange(left, length, out var leftEnd) ||
			!TryAddRange(right, length, out var rightEnd) ||
			!TryAddRange(left, offset, out var leftCurrent) ||
			!TryAddRange(right, offset, out var rightCurrent) ||
			(faultAddress != leftCurrent && faultAddress != rightCurrent) ||
			!IsGuestOwnedLazyCommitAddress(left, out _) ||
			!IsGuestOwnedLazyCommitAddress(leftEnd - 1, out _) ||
			!IsGuestOwnedLazyCommitAddress(right, out _) ||
			!IsGuestOwnedLazyCommitAddress(rightEnd - 1, out _) ||
			!TrySparseRangesEqual(
				leftCurrent,
				rightCurrent,
				length - offset))
		{
			return false;
		}

		WriteCtxU64(contextRecord, CTX_RAX, length);
		WriteCtxU64(contextRecord, CTX_RBX, 0);
		WriteCtxU64(contextRecord, CTX_RDX, 0);
		WriteCtxU64(contextRecord, CTX_RIP, loopStart + 18);
		skippedBytes = length - offset;
		return true;
	}

	private unsafe bool TrySkipSparseZeroRecordInitialization(
		void* contextRecord,
		ulong rip,
		ulong faultAddress,
		out ulong skippedBytes)
	{
		skippedBytes = 0;
		ReadOnlySpan<byte> loopPattern =
		[
			0x48, 0xC7, 0x41, 0xE0, 0x00, 0x00, 0x00, 0x00,
			0x48, 0xC7, 0x41, 0xF8, 0x00, 0x00, 0x00, 0x00,
			0x89, 0x01,
			0xC6, 0x41, 0xE8, 0x00,
			0x48, 0x83, 0xC1, 0x28,
			0x49, 0xFF, 0xCC,
			0x75, 0xE1,
		];

		ulong loopStart;
		if (*(byte*)rip == 0x48 && *((byte*)rip + 3) == 0xE0)
		{
			loopStart = rip;
		}
		else if (rip >= 8 && *(byte*)rip == 0x48 && *((byte*)rip + 3) == 0xF8)
		{
			loopStart = rip - 8;
		}
		else if (rip >= 16 && *(byte*)rip == 0x89 && *((byte*)rip + 1) == 0x01)
		{
			loopStart = rip - 16;
		}
		else if (rip >= 18 && *(byte*)rip == 0xC6 && *((byte*)rip + 1) == 0x41)
		{
			loopStart = rip - 18;
		}
		else
		{
			return false;
		}

		if (!new ReadOnlySpan<byte>((void*)loopStart, loopPattern.Length).SequenceEqual(loopPattern))
		{
			return false;
		}

		var remainingRecords = ReadCtxU64(contextRecord, CTX_R12);
		var currentPointer = ReadCtxU64(contextRecord, CTX_RCX);
		if ((uint)ReadCtxU64(contextRecord, CTX_RAX) != 0 ||
			remainingRecords == 0 ||
			currentPointer < 0x20 ||
			remainingRecords > ulong.MaxValue / 0x28)
		{
			return false;
		}

		var startAddress = currentPointer - 0x20;
		var byteCount = remainingRecords * 0x28;
		if (!TryAddRange(startAddress, byteCount, out var endAddress) ||
			faultAddress < startAddress ||
			faultAddress >= startAddress + 0x28 ||
			!IsGuestOwnedLazyCommitAddress(startAddress, out _) ||
			!IsGuestOwnedLazyCommitAddress(endAddress - 1, out _) ||
			!TryResetSparseZeroRange(startAddress, endAddress))
		{
			return false;
		}

		WriteCtxU64(contextRecord, CTX_RCX, endAddress + 0x20);
		WriteCtxU64(contextRecord, CTX_R12, 0);
		WriteCtxU64(contextRecord, CTX_RIP, loopStart + 0x51);
		skippedBytes = byteCount;
		return true;
	}

	private unsafe static bool TrySparseRangesEqual(
		ulong left,
		ulong right,
		ulong length)
	{
		if (!TryValidateSparseRange(left, length) ||
			!TryValidateSparseRange(right, length))
		{
			return false;
		}

		ulong offset = 0;
		while (offset < length)
		{
			var leftAddress = left + offset;
			var rightAddress = right + offset;
			if (VirtualQuery(
					(void*)leftAddress,
					out var leftInformation,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				VirtualQuery(
					(void*)rightAddress,
					out var rightInformation,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				!TryAddRange(
					leftInformation.BaseAddress,
					leftInformation.RegionSize,
					out var leftInformationEnd) ||
				!TryAddRange(
					rightInformation.BaseAddress,
					rightInformation.RegionSize,
					out var rightInformationEnd))
			{
				return false;
			}

			var chunkLength = Math.Min(
				length - offset,
				Math.Min(
					leftInformationEnd - leftAddress,
					rightInformationEnd - rightAddress));
			if (leftInformation.State == MEM_COMMIT &&
				rightInformation.State == MEM_COMMIT)
			{
				if (!MemoryRangesEqual(leftAddress, rightAddress, chunkLength))
				{
					return false;
				}
			}
			else if (leftInformation.State == MEM_COMMIT)
			{
				if (!MemoryRangeIsZero(leftAddress, chunkLength))
				{
					return false;
				}
			}
			else if (rightInformation.State == MEM_COMMIT &&
					 !MemoryRangeIsZero(rightAddress, chunkLength))
			{
				return false;
			}

			offset += chunkLength;
		}

		return true;
	}

	private unsafe static bool MemoryRangesEqual(
		ulong left,
		ulong right,
		ulong length)
	{
		const int chunkSize = 0x100000;
		ulong offset = 0;
		while (offset < length)
		{
			var currentLength = (int)Math.Min((ulong)chunkSize, length - offset);
			var leftBytes = new ReadOnlySpan<byte>((void*)(left + offset), currentLength);
			var rightBytes = new ReadOnlySpan<byte>((void*)(right + offset), currentLength);
			if (!leftBytes.SequenceEqual(rightBytes))
			{
				return false;
			}

			offset += (ulong)currentLength;
		}

		return true;
	}

	private unsafe static bool MemoryRangeIsZero(ulong address, ulong length)
	{
		const int chunkSize = 0x100000;
		ulong offset = 0;
		while (offset < length)
		{
			var currentLength = (int)Math.Min((ulong)chunkSize, length - offset);
			var bytes = new ReadOnlySpan<byte>((void*)(address + offset), currentLength);
			if (bytes.IndexOfAnyExcept((byte)0) >= 0)
			{
				return false;
			}

			offset += (ulong)currentLength;
		}

		return true;
	}

	private unsafe static bool TryCopySparseRange(
		ulong source,
		ulong destination,
		ulong length)
	{
		if (!TryValidateSparseRange(source, length) ||
			!TryValidateSparseRange(destination, length))
		{
			return false;
		}

		ulong offset = 0;
		while (offset < length)
		{
			var sourceAddress = source + offset;
			if (VirtualQuery(
					(void*)sourceAddress,
					out var sourceInformation,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				!TryAddRange(
					sourceInformation.BaseAddress,
					sourceInformation.RegionSize,
					out var sourceInformationEnd))
			{
				return false;
			}

			var chunkLength = Math.Min(
				length - offset,
				sourceInformationEnd - sourceAddress);
			var destinationAddress = destination + offset;
			if (sourceInformation.State == MEM_RESERVE)
			{
				if (!TryResetSparseZeroRange(
						destinationAddress,
						destinationAddress + chunkLength))
				{
					return false;
				}
			}
			else if (sourceInformation.State == MEM_COMMIT)
			{
				if (VirtualAlloc(
						(void*)destinationAddress,
						(nuint)chunkLength,
						MEM_COMMIT,
						PAGE_READWRITE) == null)
				{
					return false;
				}

				Buffer.MemoryCopy(
					(void*)sourceAddress,
					(void*)destinationAddress,
					(nuint)chunkLength,
					(nuint)chunkLength);
			}
			else
			{
				return false;
			}

			offset += chunkLength;
		}

		return true;
	}

	private unsafe static bool TryValidateSparseRange(ulong address, ulong length)
	{
		if (!TryAddRange(address, length, out var endAddress))
		{
			return false;
		}

		var cursor = address;
		while (cursor < endAddress)
		{
			if (VirtualQuery(
					(void*)cursor,
					out var information,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				information.RegionSize == 0 ||
				information.State is not (MEM_COMMIT or MEM_RESERVE) ||
				!TryAddRange(
					information.BaseAddress,
					information.RegionSize,
					out var informationEnd) ||
				informationEnd <= cursor)
			{
				return false;
			}

			cursor = Math.Min(endAddress, informationEnd);
		}

		return true;
	}

	private unsafe static bool TryResetSparseZeroRange(ulong startAddress, ulong endAddress)
	{
		var cursor = startAddress;
		while (cursor < endAddress)
		{
			if (VirtualQuery(
					(void*)cursor,
					out var information,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				information.RegionSize == 0 ||
				!TryAddRange(information.BaseAddress, information.RegionSize, out var informationEnd) ||
				informationEnd <= cursor ||
				information.State is not (MEM_COMMIT or MEM_RESERVE))
			{
				return false;
			}

			cursor = Math.Min(endAddress, informationEnd);
		}

		cursor = startAddress;
		while (cursor < endAddress)
		{
			if (VirtualQuery(
					(void*)cursor,
					out var information,
					(nuint)sizeof(MEMORY_BASIC_INFORMATION64)) == 0 ||
				!TryAddRange(information.BaseAddress, information.RegionSize, out var informationEnd))
			{
				return false;
			}

			var rangeEnd = Math.Min(endAddress, informationEnd);
			if (information.State == MEM_COMMIT &&
				!TryResetCommittedZeroRange(cursor, rangeEnd))
			{
				return false;
			}

			cursor = rangeEnd;
		}

		return true;
	}

	private unsafe static bool TryResetCommittedZeroRange(ulong startAddress, ulong endAddress)
	{
		const ulong pageSize = 0x1000;
		var fullPageStart = (startAddress + pageSize - 1) & ~(pageSize - 1);
		var fullPageEnd = endAddress & ~(pageSize - 1);

		var prefixEnd = Math.Min(endAddress, fullPageStart);
		if (prefixEnd > startAddress &&
			!TryZeroCommittedBytes(startAddress, prefixEnd - startAddress))
		{
			return false;
		}

		if (fullPageEnd > fullPageStart &&
			!VirtualFree(
				(void*)fullPageStart,
				(nuint)(fullPageEnd - fullPageStart),
				MEM_DECOMMIT))
		{
			return false;
		}

		var suffixStart = Math.Max(startAddress, fullPageEnd);
		return suffixStart >= endAddress ||
			   TryZeroCommittedBytes(suffixStart, endAddress - suffixStart);
	}

	private unsafe static bool TryZeroCommittedBytes(ulong address, ulong length)
	{
		if (length == 0)
		{
			return true;
		}

		uint oldProtection;
		if (!VirtualProtect(
				(void*)address,
				(nuint)length,
				PAGE_READWRITE,
				&oldProtection))
		{
			return false;
		}

		NativeMemory.Clear((void*)address, (nuint)length);
		return VirtualProtect(
			(void*)address,
			(nuint)length,
			oldProtection,
			&oldProtection);
	}

	private static bool TryAddRange(ulong address, ulong length, out ulong endAddress)
	{
		if (length > ulong.MaxValue - address)
		{
			endAddress = 0;
			return false;
		}

		endAddress = address + length;
		return true;
	}

	private static bool ShouldTraceLazyCommit(int traceIndex)
	{
		if (string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_LOG_LAZY_COMMIT"), "1", StringComparison.Ordinal))
		{
			return true;
		}

		return traceIndex <= 16 || traceIndex % 256 == 0;
	}

	private static uint ResolveLazyCommitProtection(ulong accessType, uint allocationProtect)
	{
		if (accessType == 8 || (allocationProtect & 0xF0) != 0)
		{
			return 64u;
		}
		return 4u;
	}
}
