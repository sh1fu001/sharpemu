// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers.Binary;
using System.IO;

namespace SharpEmu.Libs.Agc;

/// <summary>
/// On-disk cache of translated shaders, keyed by the source program's stage and hash. Translating GCN to
/// SPIR-V is expensive, so a title only pays for it once; subsequent runs (and repeated binds within a run)
/// load the cached SPIR-V. Entries carry a magic/version header so a translator change transparently
/// invalidates stale blobs.
/// </summary>
internal sealed class ShaderTranslationCache
{
    private const uint CacheMagic = 0x53_45_53_31; // "SES1"
    private const uint CacheVersion = 1;

    private readonly string _directory;

    public ShaderTranslationCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public bool TryLoad(ShaderStage stage, ulong hash, out byte[] spirv)
    {
        spirv = Array.Empty<byte>();
        var path = EntryPath(stage, hash);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < HeaderSize ||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes) != CacheMagic ||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(sizeof(uint))) != CacheVersion)
            {
                return false;
            }

            spirv = bytes[HeaderSize..];
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Store(ShaderStage stage, ulong hash, ReadOnlySpan<byte> spirv)
    {
        Directory.CreateDirectory(_directory);
        var payload = new byte[HeaderSize + spirv.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, CacheMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(sizeof(uint)), CacheVersion);
        spirv.CopyTo(payload.AsSpan(HeaderSize));

        // Write to a temporary file first so a concurrent reader never sees a half-written entry.
        var finalPath = EntryPath(stage, hash);
        var tempPath = finalPath + ".tmp" + Environment.CurrentManagedThreadId.ToString("X");
        File.WriteAllBytes(tempPath, payload);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private const int HeaderSize = 2 * sizeof(uint);

    private string EntryPath(ShaderStage stage, ulong hash) =>
        Path.Combine(_directory, $"{stage.ToString().ToLowerInvariant()}_{hash:X16}.spv");
}
