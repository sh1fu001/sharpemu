// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Text;

namespace SharpEmu.CLI;

/// <summary>
/// Mirrors everything written to <see cref="Console.Out"/> and <see cref="Console.Error"/> into a
/// temporary file so it can be saved as the session <c>boot.log</c>. The console still receives every
/// byte (so an external <c>2&gt;&amp;1 | tee</c> keeps working); this just also keeps a copy on disk.
/// </summary>
internal sealed class BootLogCapture : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly StreamWriter _file;
    private readonly string _tempPath;
    private bool _disposed;

    private BootLogCapture(string tempPath, StreamWriter file, TextWriter originalOut, TextWriter originalError)
    {
        _tempPath = tempPath;
        _file = file;
        _originalOut = originalOut;
        _originalError = originalError;
    }

    public static BootLogCapture Start()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sharpemu-boot-{Guid.NewGuid():N}.log");
        var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        var file = new StreamWriter(stream) { AutoFlush = false };
        var shared = TextWriter.Synchronized(file);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(new TeeTextWriter(originalOut, shared));
        Console.SetError(new TeeTextWriter(originalError, shared));
        return new BootLogCapture(tempPath, file, originalOut, originalError);
    }

    public string ReadCapturedText()
    {
        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
            lock (_file)
            {
                _file.Flush();
            }

            // The writer handle is still open, so read with a compatible share mode instead of
            // File.ReadAllText (which requests FileShare.Read and would hit a sharing violation).
            using var readStream = new FileStream(_tempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(readStream);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        try
        {
            _file.Dispose();
        }
        catch
        {
            // Best effort: diagnostics must never take the process down.
        }

        try
        {
            File.Delete(_tempPath);
        }
        catch
        {
            // Best effort.
        }
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _primary;
        private readonly TextWriter _secondary;

        public TeeTextWriter(TextWriter primary, TextWriter secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public override Encoding Encoding => _primary.Encoding;

        public override void Write(char value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }

        public override void Write(string? value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _primary.WriteLine(value);
            _secondary.WriteLine(value);
        }

        public override void WriteLine()
        {
            _primary.WriteLine();
            _secondary.WriteLine();
        }

        public override void Flush()
        {
            _primary.Flush();
            _secondary.Flush();
        }
    }
}
