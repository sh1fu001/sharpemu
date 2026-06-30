// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text;
using SharpEmu.Core;
using SharpEmu.Core.Loader;
using Xunit;

namespace SharpEmu.Tests;

public sealed class ParamLoaderTests
{
    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public FakeFileSystem Add(string path, string contents)
        {
            _files[path] = Encoding.UTF8.GetBytes(contents);
            return this;
        }

        public bool Exists(string path) => _files.ContainsKey(path);

        public bool TryReadAllBytes(string path, out byte[] data)
        {
            if (_files.TryGetValue(path, out var bytes))
            {
                data = bytes;
                return true;
            }

            data = Array.Empty<byte>();
            return false;
        }
    }

    private static (string? Title, string? TitleId, string? Version) Read(string json)
        => Ps5ParamJsonReader.TryReadPs5Param(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void ReadsTitleIdAndDefaultLanguageTitle()
    {
        var (title, titleId, version) = Read("""
        {
            "titleId": "PPSA01234",
            "contentVersion": "01.02",
            "localizedParameters": {
                "defaultLanguage": "fr-FR",
                "fr-FR": { "titleName": "Mon Jeu" },
                "en-US": { "titleName": "My Game" }
            }
        }
        """);

        Assert.Equal("PPSA01234", titleId);
        Assert.Equal("01.02", version);
        Assert.Equal("Mon Jeu", title);
    }

    [Fact]
    public void FallsBackToEnUsWhenNoDefaultLanguage()
    {
        var (title, _, _) = Read("""
        {
            "titleId": "PPSA00001",
            "localizedParameters": {
                "en-US": { "titleName": "Fallback Title" }
            }
        }
        """);

        Assert.Equal("Fallback Title", title);
    }

    [Fact]
    public void FallsBackToDiscLocalizedParameters()
    {
        var (title, _, _) = Read("""
        {
            "titleId": "PPSA00002",
            "disc": {
                "localizedParameters": {
                    "defaultLanguage": "en-US",
                    "en-US": { "titleName": "Disc Title" }
                }
            }
        }
        """);

        Assert.Equal("Disc Title", title);
    }

    [Fact]
    public void VersionPrefersContentThenMasterThenTarget()
    {
        Assert.Equal("CONTENT", Read("""{ "contentVersion": "CONTENT", "masterVersion": "MASTER", "targetContentVersion": "TARGET" }""").Version);
        Assert.Equal("MASTER", Read("""{ "masterVersion": "MASTER", "targetContentVersion": "TARGET" }""").Version);
        Assert.Equal("TARGET", Read("""{ "targetContentVersion": "TARGET" }""").Version);
    }

    [Fact]
    public void EmptyOrInvalidJson_ReturnsAllNull()
    {
        Assert.Equal((null, null, null), Ps5ParamJsonReader.TryReadPs5Param(Array.Empty<byte>()));
        Assert.Equal((null, null, null), Read("not json at all"));
    }

    [Fact]
    public void FileSystemOverload_MissingFile_ReturnsAllNull()
    {
        var fs = new FakeFileSystem();
        Assert.Equal((null, null, null), Ps5ParamJsonReader.TryReadPs5Param(fs, "/app0/param.json"));
    }

    [Fact]
    public void FileSystemOverload_ReadsExistingFile()
    {
        var fs = new FakeFileSystem().Add(
            "/app0/param.json",
            """{ "titleId": "PPSA09999", "contentVersion": "09.99" }""");

        var (_, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(fs, "/app0/param.json");
        Assert.Equal("PPSA09999", titleId);
        Assert.Equal("09.99", version);
    }
}
