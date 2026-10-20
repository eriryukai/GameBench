// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameBench.Editor.Native.Interop;

// Orchestrates one engine connection: pipe server -> spawn Engine.exe ->
// accept -> Hello handshake -> wait for Ready. After that a monitor loop keeps
// dispatching engine messages and raises Disconnected exactly once when the
// pipe or the process goes away. ReconnectAsync respawns a fresh engine.
public sealed class EngineSession : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownSendTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MonitorJoinTimeout = TimeSpan.FromSeconds(1);

    private NamePipeServer? _server;
    private EngineProcess? _process;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private TaskCompletionSource? _readySignal;
    private bool _stopping;
    private bool _disconnectedRaised;

    // Raised on a background thread; consumers marshal to the UI thread.
    public event EventHandler<EngineProgress>? Progress;
    public event EventHandler<string>? Log;
    public event EventHandler<string>? Disconnected;

    public bool IsConnected { get; private set; }

    public HelloMessage? EngineHello { get; private set; }

    public string PipeName { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = false;
        _disconnectedRaised = false;

        PipeName = $"gamebench-editor-{Environment.ProcessId}-{Guid.NewGuid():N}";
        _server = new NamePipeServer(PipeName);
        _readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var processExited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        _process = new EngineProcess();
        _process.Exited += exitCode =>
        {
            processExited.TrySetResult(exitCode);

            if (IsConnected && !_stopping)
            {
                RaiseDisconnected($"the engine exited (code {exitCode})");
            }
        };

        try
        {
            // Server first, then the child, so the engine can connect immediately.
            var acceptTask = _server.WaitForConnectionAsync(cancellationToken);
            _process.Start($"--editor --ipc-pipe={PipeName}");

            await WaitForConnectionAsync(acceptTask, processExited.Task, cancellationToken).ConfigureAwait(false);
            await PerformHandshakeAsync(cancellationToken).ConfigureAwait(false);

            _monitorCts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorAsync(_monitorCts.Token), CancellationToken.None);

            await WaitForReadyAsync(processExited.Task, cancellationToken).ConfigureAwait(false);

            IsConnected = true;
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ShutdownAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task WaitForConnectionAsync(Task acceptTask, Task processExited, CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(ConnectTimeout, cancellationToken);
        var completed = await Task.WhenAny(acceptTask, processExited, timeout).ConfigureAwait(false);

        if (completed == processExited)
        {
            throw new EngineStartupException(BuildStartupFailure("The engine exited before connecting to the editor."));
        }

        if (completed == timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new EngineStartupException(BuildStartupFailure("Timed out waiting for the engine to connect."));
        }

        await acceptTask.ConfigureAwait(false);
    }

    private async Task PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeCts.CancelAfter(HandshakeTimeout);

        IncomingFrame frame;
        try
        {
            frame = await _server!.ReceiveAsync(handshakeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is OperationCanceledException or EndOfStreamException or IOException)
        {
            throw new EngineStartupException(BuildStartupFailure("The engine dropped the connection during the handshake."));
        }

        if (frame.Type != InteropMessageType.Hello)
        {
            throw new EngineStartupException($"The engine sent '{frame.Type}' before Hello.");
        }

        var hello = TryDeserialize<HelloMessage>(frame.Json);
        if (hello is null)
        {
            throw new EngineStartupException("The engine sent an unreadable Hello message.");
        }

        if (hello.Protocol != InteropProtocol.Version)
        {
            var reason = $"Protocol mismatch: editor v{InteropProtocol.Version}, engine v{hello.Protocol}. Rebuild the engine.";
            await SafeSendAsync(InteropMessageType.Reject, new RejectMessage { Reason = reason }, CancellationToken.None).ConfigureAwait(false);
            throw new EngineStartupException(reason);
        }

        await _server!.SendAsync(
            InteropMessageType.HelloAck,
            new HelloAckMessage
            {
                Protocol = InteropProtocol.Version,
                SessionId = PipeName,
            },
            cancellationToken).ConfigureAwait(false);

        EngineHello = hello;
    }

    private async Task WaitForReadyAsync(Task processExited, CancellationToken cancellationToken)
    {
        var readyTask = _readySignal!.Task;
        var timeout = Task.Delay(ReadyTimeout, cancellationToken);
        var completed = await Task.WhenAny(readyTask, processExited, timeout).ConfigureAwait(false);

        if (completed == processExited)
        {
            throw new EngineStartupException(BuildStartupFailure("The engine exited during startup."));
        }

        if (completed == timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new EngineStartupException(BuildStartupFailure("Timed out waiting for the engine to finish initializing."));
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IncomingFrame frame;
            try
            {
                frame = await _server!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (_stopping || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                RaiseDisconnected(error is EndOfStreamException
                    ? "the engine closed the connection"
                    : $"pipe error: {error.Message}");
                return;
            }

            switch (frame.Type)
            {
                case InteropMessageType.Progress:
                {
                    var progress = TryDeserialize<ProgressMessage>(frame.Json);
                    if (progress is not null)
                    {
                        Progress?.Invoke(this, new EngineProgress(progress.Step, progress.Message, progress.Index, progress.Total));
                    }
                    break;
                }
                case InteropMessageType.Log:
                {
                    var log = TryDeserialize<LogMessage>(frame.Json);
                    if (log is not null)
                    {
                        Log?.Invoke(this, log.Message);
                    }
                    break;
                }
                case InteropMessageType.Ready:
                    _readySignal?.TrySetResult();
                    break;
            }
        }
    }

    private async Task StopAsync()
    {
        _stopping = true;
        IsConnected = false;

        if (_server is not null)
        {
            using var shutdownCts = new CancellationTokenSource(ShutdownSendTimeout);
            await SafeSendAsync(InteropMessageType.Shutdown, null, shutdownCts.Token).ConfigureAwait(false);
        }

        _monitorCts?.Cancel();

        // Disposing the server unblocks a pending ReceiveAsync in the monitor.
        _server?.Dispose();
        _server = null;

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.WaitAsync(MonitorJoinTimeout).ConfigureAwait(false);
            }
            catch
            {
            }

            _monitorTask = null;
        }

        if (_process is not null)
        {
            await _process.KillAsync().ConfigureAwait(false);
            _process.Dispose();
            _process = null;
        }

        _monitorCts?.Dispose();
        _monitorCts = null;
        _readySignal = null;
    }

    private async Task SafeSendAsync(InteropMessageType type, object? payload, CancellationToken cancellationToken)
    {
        if (_server is null)
        {
            return;
        }

        try
        {
            await _server.SendAsync(type, payload, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best effort: the kill in StopAsync is the real guarantee.
        }
    }

    private void RaiseDisconnected(string reason)
    {
        if (_stopping || _disconnectedRaised)
        {
            return;
        }

        _disconnectedRaised = true;
        IsConnected = false;
        Disconnected?.Invoke(this, reason);
    }

    private string BuildStartupFailure(string message)
    {
        var tail = _process?.StdErrTail;
        if (string.IsNullOrWhiteSpace(tail))
        {
            tail = _process?.StdOutTail;
        }

        return string.IsNullOrWhiteSpace(tail)
            ? message
            : $"{message}{Environment.NewLine}{tail}";
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record EngineProgress(string Step, string Message, int Index, int Total);
