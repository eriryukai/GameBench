// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameBench.Editor.Native.Interop;

// The editor-side half of the engine IPC: a one-instance byte-mode named pipe
// that frames every message as
//     uint32 length | uint8 type | UTF-8 JSON (length - 1 bytes)
// Engine.exe connects as the client. This class only moves frames around; the
// protocol conversation itself lives in EngineSession.
internal sealed class NamePipeServer : IDisposable
{
    private const int MaxFrameSize = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly NamedPipeServerStream _server;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public NamePipeServer(string pipeName)
    {
        PipeName = pipeName;
        _server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    public string PipeName { get; }

    public Task WaitForConnectionAsync(CancellationToken cancellationToken) =>
        _server.WaitForConnectionAsync(cancellationToken);

    public async Task SendAsync(InteropMessageType type, object? payload, CancellationToken cancellationToken)
    {
        var json = payload is null
            ? Array.Empty<byte>()
            : JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);

        var frame = new byte[5 + json.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(1 + json.Length));
        frame[4] = (byte)type;
        json.CopyTo(frame.AsSpan(5));

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _server.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _server.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IncomingFrame> ReceiveAsync(CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length == 0 || length > MaxFrameSize)
        {
            throw new InvalidDataException($"Invalid interop frame length: {length}.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        var type = (InteropMessageType)payload[0];
        var json = payload.Length > 1 ? Encoding.UTF8.GetString(payload, 1, payload.Length - 1) : "{}";
        return new IncomingFrame(type, json);
    }

    private async Task ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await _server.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("The engine closed the pipe.");
            }

            read += count;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _server.Dispose();
        _writeLock.Dispose();
    }
}

internal readonly record struct IncomingFrame(InteropMessageType Type, string Json);
