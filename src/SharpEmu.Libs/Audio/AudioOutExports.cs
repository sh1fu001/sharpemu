// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using SharpEmu.HLE;
using SharpEmu.Logging;

namespace SharpEmu.Libs.Audio;

public static class AudioOutExports
{
    private static readonly SharpEmuLogger Log = SharpEmuLog.For("Audio");

    private const ushort OutputConnectedPrimary = 0x01;
    private const ushort OutputConnectedTertiary = 0x04;
    private const ushort OutputConnectedHeadphone = 0x40;
    private const ushort OutputConnectedExternal = 0x80;
    private const int PortStateSize = 0x20;

    private static int _nextHandle;
    private static readonly ConcurrentDictionary<int, AudioPort> _ports = new();

    private sealed class AudioPort
    {
        public required int Type { get; init; }
        public required byte Channels { get; init; }
        public required uint BufferFrames { get; init; }
        public required uint SampleRate { get; init; }
        public long NextOutputTimestamp { get; set; }
        public object OutputGate { get; } = new();
    }

    [SysAbiExport(
        Nid = "JfEPXVxhFqA",
        ExportName = "sceAudioOutInit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutInit(CpuContext ctx)
    {
        Log.Info("sceAudioOutInit()");
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "ekNvsT22rsY",
        ExportName = "sceAudioOutOpen",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutOpen(CpuContext ctx)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        var portType = unchecked((int)ctx[CpuRegister.Rsi]);
        var bufferFrames = unchecked((uint)ctx[CpuRegister.Rcx]);
        var sampleRate = unchecked((uint)ctx[CpuRegister.R8]);
        var channels = GetChannelCount(portType, unchecked((uint)ctx[CpuRegister.R9]));
        Log.Info(
            $"sceAudioOutOpen(user={ctx[CpuRegister.Rdi]:X} type={portType} index={unchecked((int)ctx[CpuRegister.Rdx])} " +
            $"len={bufferFrames} freq={sampleRate} param=0x{ctx[CpuRegister.R9]:X8}) -> handle={handle}");
        _ports[handle] = new AudioPort
        {
            Type = portType,
            Channels = channels,
            BufferFrames = bufferFrames == 0 ? 1024u : bufferFrames,
            SampleRate = sampleRate == 0 ? 48_000u : sampleRate,
        };
        ctx[CpuRegister.Rax] = unchecked((uint)handle);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "QOQtbeDqsT4",
        ExportName = "sceAudioOutOutput",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutOutput(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        if (!_ports.TryGetValue(handle, out var port))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        long delayTicks;
        lock (port.OutputGate)
        {
            var now = Stopwatch.GetTimestamp();
            delayTicks = Math.Max(0, port.NextOutputTimestamp - now);
            var bufferTicks = Math.Max(
                1,
                (long)((double)port.BufferFrames * Stopwatch.Frequency / port.SampleRate));
            port.NextOutputTimestamp = Math.Max(now, port.NextOutputTimestamp) + bufferTicks;
        }

        if (delayTicks > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds((double)delayTicks / Stopwatch.Frequency));
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "s1--uE9mBFw",
        ExportName = "sceAudioOutClose",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutClose(CpuContext ctx)
    {
        _ports.TryRemove(unchecked((int)ctx[CpuRegister.Rdi]), out _);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "GrQ9s4IrNaQ",
        ExportName = "sceAudioOutGetPortState",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutGetPortState(CpuContext ctx)
    {
        var handle = unchecked((int)ctx[CpuRegister.Rdi]);
        var stateAddress = ctx[CpuRegister.Rsi];
        if (stateAddress == 0 || !_ports.TryGetValue(handle, out var port))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> state = stackalloc byte[PortStateSize];
        BinaryPrimitives.WriteUInt16LittleEndian(state, GetOutputState(port.Type));
        state[0x02] = port.Channels;
        BinaryPrimitives.WriteInt16LittleEndian(
            state[0x04..],
            port.Type == 4 ? (short)127 : (short)-1);

        if (!ctx.Memory.TryWrite(stateAddress, state))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "b+uAV89IlxE",
        ExportName = "sceAudioOutSetUsbVolume",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAudioOut")]
    public static int AudioOutSetUsbVolume(CpuContext ctx)
    {
        // No USB audio device is emulated; accept the volume request as a no-op.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static byte GetChannelCount(int portType, uint format)
    {
        if (portType is 2 or 3 or 4)
        {
            return 1;
        }

        return (format & 0xFF) switch
        {
            0 or 3 => 1,
            2 or 5 or 6 or 7 => 2,
            _ => 2,
        };
    }

    private static ushort GetOutputState(int portType) =>
        portType switch
        {
            2 or 3 => OutputConnectedHeadphone,
            4 => OutputConnectedTertiary,
            127 => OutputConnectedExternal,
            _ => OutputConnectedPrimary,
        };

    private static int SetReturn(CpuContext ctx, OrbisGen2Result result)
    {
        var value = (int)result;
        ctx[CpuRegister.Rax] = unchecked((ulong)value);
        return value;
    }
}
