// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MortarEngine.Editor.Native.EditorIpc;

// Managed side of the editor control channel. The engine is launched with the
// pipe path and connects as a client; this class owns the pipe server, the
// engine process (when it launched one), and handle duplication.
internal sealed class EditorSession : IAsyncDisposable
{
    private const int ConnectionTimeoutSeconds = 15;

    private readonly string _pipeName;
    private readonly string _pipePath;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private NamedPipeServerStream? _server;
    private Process? _engineProcess;
    private Task? _readTask;
    private string? _launchError;
    private bool _disposed;
    private bool _readLoopStarted;

    public EditorSession()
    {
        _pipeName = "MortarEngine.Editor." + Guid.NewGuid().ToString("N");
        _pipePath = @"\\.\pipe\" + _pipeName;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public HelloEngine Hello { get; private set; }
    public bool HasHello { get; private set; }
    public bool IsConnected => _server?.IsConnected == true;

    public event Action<uint, ulong>? FrameReady;
    public event Action? Disconnected;

    public static async Task<EditorSession> StartAsync(
        byte[]? deviceLuid,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var session = new EditorSession();
        try
        {
            await session.ConnectAsync(deviceLuid, width, height, cancellationToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public async Task ConnectAsync(
        byte[]? deviceLuid,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        if (_server != null)
        {
            throw new InvalidOperationException("The editor session is already connected.");
        }

        _server = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.CurrentUserOnly,
            65536,
            65536);

        TryStartEngine(deviceLuid, width, height);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

        try
        {
            await _server.WaitForConnectionAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = string.IsNullOrWhiteSpace(_launchError)
                ? "Make sure Engine.exe exists next to the editor or is running with the same pipe path."
                : "Engine launch failed: " + _launchError;
            throw new TimeoutException("The engine did not connect to the editor pipe. " + detail);
        }

        var (header, payload) = await ReadMessageAsync(_server, timeout.Token);
        if (header.Type != (ushort)EditorProtocol.MessageType.HelloEngine)
        {
            throw new InvalidDataException($"Expected HelloEngine, got message type {header.Type}.");
        }

        if (payload.Length < Marshal.SizeOf<HelloEngine>())
        {
            throw new InvalidDataException("HelloEngine payload is truncated.");
        }

        Hello = MemoryMarshal.Read<HelloEngine>(payload);
        if (Hello.SlotCount != EditorProtocol.MaxSlots ||
            Hello.SharedHandle0 == 0 ||
            Hello.SharedHandle1 == 0 ||
            Hello.SharedHandle2 == 0)
        {
            throw new InvalidDataException("HelloEngine did not provide three shared texture handles.");
        }

        HasHello = true;
    }

    // Start reading only after the viewport has subscribed to FrameReady.
    // Otherwise early frames can be consumed and dropped before there is any
    // handler, leaving all three native slots permanently "ready" with no ack.
    public void StartReadLoop()
    {
        if (_readLoopStarted || _disposed)
        {
            return;
        }

        _readLoopStarted = true;
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    public IntPtr DuplicateSharedHandle(ulong handleValue)
    {
        if (!HasHello || handleValue == 0)
        {
            throw new InvalidOperationException("No shared texture handle is available yet.");
        }

        var sourceProcess = NativeMethods.OpenProcess(
            NativeMethods.ProcessDuplicateHandle,
            false,
            unchecked((int)Hello.ProcessId));

        if (sourceProcess == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed for the engine process.");
        }

        try
        {
            if (!NativeMethods.DuplicateHandle(
                    sourceProcess,
                    unchecked((IntPtr)(long)handleValue),
                    NativeMethods.GetCurrentProcess(),
                    out var localHandle,
                    0,
                    false,
                    NativeMethods.DuplicateSameAccess))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateHandle failed.");
            }

            return localHandle;
        }
        finally
        {
            NativeMethods.CloseHandle(sourceProcess);
        }
    }

    public Task SendFrameAckAsync(uint slot, ulong frameId)
    {
        var payload = new FrameAckPayload
        {
            Slot = slot,
            FrameId = frameId,
        };

        return SendAsync(EditorProtocol.MessageType.FrameAck, payload, frameId, _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_server is { IsConnected: true } && !_cts.IsCancellationRequested)
            {
                using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
                await SendAsync(
                    EditorProtocol.MessageType.Shutdown,
                    new ShutdownPayload(),
                    0,
                    shutdownTimeout.Token);
            }
        }
        catch
        {
            // Best effort during shutdown.
        }

        _cts.Cancel();

        try
        {
            _server?.Dispose();
        }
        catch
        {
            // Best effort.
        }

        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
                // The read loop is expected to unwind when the pipe closes.
            }
        }

        KillEngineProcess();
        _engineProcess?.Dispose();
        _engineProcess = null;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

        _writeLock.Dispose();
        _cts.Dispose();
    }

    private void OnProcessExit(object? sender, EventArgs args) => KillEngineProcess();

    private void KillEngineProcess()
    {
        if (_engineProcess is not { HasExited: false })
        {
            return;
        }

        try
        {
            _engineProcess.Kill(entireProcessTree: true);
            _engineProcess.WaitForExit(1000);
        }
        catch
        {
            // The process may have exited between the check and the kill.
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _server != null)
            {
                var (header, payload) = await ReadMessageAsync(_server, cancellationToken);

                switch ((EditorProtocol.MessageType)header.Type)
                {
                    case EditorProtocol.MessageType.FrameReady:
                        if (payload.Length >= Marshal.SizeOf<FrameReadyPayload>())
                        {
                            var ready = MemoryMarshal.Read<FrameReadyPayload>(payload);
                            FrameReady?.Invoke(ready.Slot, header.FrameId);
                        }
                        break;

                    case EditorProtocol.MessageType.Shutdown:
                        Disconnected?.Invoke();
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal session shutdown.
        }
        catch
        {
            if (!_disposed)
            {
                Disconnected?.Invoke();
            }
        }
    }

    private async Task<(MessageHeader Header, byte[] Payload)> ReadMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var headerBytes = new byte[EditorProtocol.HeaderSize];
        await ReadExactAsync(stream, headerBytes, cancellationToken);

        var header = MemoryMarshal.Read<MessageHeader>(headerBytes);
        if (header.Magic != EditorProtocol.Magic ||
            header.ProtocolVersion != EditorProtocol.Version ||
            header.PayloadSize > EditorProtocol.MaxPayloadSize)
        {
            throw new InvalidDataException("Invalid editor IPC message header.");
        }

        var payload = new byte[header.PayloadSize];
        if (payload.Length > 0)
        {
            await ReadExactAsync(stream, payload, cancellationToken);
        }

        return (header, payload);
    }

    private static async Task ReadExactAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.Slice(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The editor pipe closed while reading a message.");
            }

            offset += read;
        }
    }

    private async Task SendAsync<T>(
        EditorProtocol.MessageType type,
        T payload,
        ulong frameId,
        CancellationToken cancellationToken)
        where T : struct
    {
        var payloadBytes = new byte[Marshal.SizeOf<T>()];
        MemoryMarshal.Write(payloadBytes, in payload);

        await SendAsync(type, payloadBytes, frameId, cancellationToken);
    }

    private async Task SendAsync(
        EditorProtocol.MessageType type,
        byte[] payload,
        ulong frameId,
        CancellationToken cancellationToken)
    {
        var server = _server;
        if (server == null || !server.IsConnected)
        {
            return;
        }

        var header = new MessageHeader
        {
            Magic = EditorProtocol.Magic,
            ProtocolVersion = EditorProtocol.Version,
            Type = (ushort)type,
            PayloadSize = (uint)payload.Length,
            FrameId = frameId,
        };

        var buffer = new byte[EditorProtocol.HeaderSize + payload.Length];
        MemoryMarshal.Write(buffer.AsSpan(0, EditorProtocol.HeaderSize), in header);
        payload.CopyTo(buffer.AsSpan(EditorProtocol.HeaderSize));

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await server.WriteAsync(buffer, cancellationToken);
            await server.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void TryStartEngine(byte[]? deviceLuid, int width, int height)
    {
        var enginePath = Environment.GetEnvironmentVariable("MORTARENGINE_ENGINE_PATH");
        if (string.IsNullOrWhiteSpace(enginePath))
        {
            enginePath = Path.Combine(AppContext.BaseDirectory, "Engine.exe");
        }

        if (!File.Exists(enginePath))
        {
            // The user may be running the engine separately. The pipe server still
            // waits for it, so this is not fatal until the connection timeout.
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            WorkingDirectory = Path.GetDirectoryName(enginePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("--editor");
        startInfo.ArgumentList.Add("--pipe=" + _pipePath);
        startInfo.ArgumentList.Add("--width=" + width.ToString());
        startInfo.ArgumentList.Add("--height=" + height.ToString());
        startInfo.ArgumentList.Add("--fps=60");

        if (deviceLuid is { Length: >= 8 })
        {
            var luid = BitConverter.ToUInt64(deviceLuid, 0);
            if (luid != 0)
            {
                startInfo.ArgumentList.Add($"--adapter-luid=0x{luid:X16}");
            }
        }

        try
        {
            _engineProcess = Process.Start(startInfo);
        }
        catch (Exception error)
        {
            // The user may still start the engine manually (or attach to one
            // already running). Wait for the pipe instead of failing immediately.
            _launchError = error.Message;
            _engineProcess = null;
        }
    }
}
