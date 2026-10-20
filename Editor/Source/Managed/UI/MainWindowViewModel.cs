// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Dock.Model.Controls;
using Dock.Model.Core;
using GameBench.Editor.Managed.Core;
using GameBench.Editor.Managed.Docking;
using GameBench.Editor.Managed.UI.Caption;
using GameBench.Editor.Managed.UI.TitleBar;
using GameBench.Editor.Managed.UI.Toolbar;
using GameBench.Editor.Managed.ViewModels;
using GameBench.Editor.Native.Interop;
using ReactiveUI;

namespace GameBench.Editor.Managed.UI;

// Root view model for the editor shell. It composes the region view models and owns the
// dock layout, so MainWindow itself stays a thin composition surface. It also listens to
// the engine session: logs land in the notification bell, and a disconnect posts a
// notification with a "Reconnect" action.
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly EngineSession _session;
    private bool _reconnecting;

    public MainWindowViewModel(WindowCaptionViewModel caption, EngineSession session)
    {
        _session = session;

        TitleBar = new TitleBarViewModel(State, caption);
        Transport = new TransportViewModel(State);
        CreateLayout();

        ReconnectCommand = ReactiveCommand.CreateFromTask(ReconnectAsync);

        session.Disconnected += OnEngineDisconnected;
        session.Log += OnEngineLog;

        if (session.IsConnected)
        {
            State.AddNotification($"Engine connected ({session.EngineHello?.EngineVersion ?? "unknown version"}).");
        }
    }

    public EditorState State { get; } = new();

    public string ProjectName => "GameBench";

    public string Title => $"MortarEngine Editor - {ProjectName}";

    public TitleBarViewModel TitleBar { get; }

    public TransportViewModel Transport { get; }

    public ICommand ReconnectCommand { get; }

    public EditorDockFactory EditorFactory { get; private set; } = null!;

    public IFactory Factory { get; private set; } = null!;

    public IRootDock Layout { get; private set; } = null!;

    public void ReportStatus(string status) => State.ReportStatus(status);

    public Task ResetLayoutAsync()
    {
        CloseLayout();
        CreateLayout();
        return Task.CompletedTask;
    }

    // Called when the editor window closes: the engine child is shut down with it.
    public async Task ShutdownAsync()
    {
        CloseLayout();

        _session.Disconnected -= OnEngineDisconnected;
        _session.Log -= OnEngineLog;
        await _session.DisposeAsync();
    }

    private void CloseLayout()
    {
        if (Layout is IDock dock && dock.Close.CanExecute(null))
            dock.Close.Execute(null);
    }

    private void OnEngineDisconnected(object? sender, string reason) =>
        Dispatcher.UIThread.Post(() =>
            State.AddNotification($"Engine disconnected: {reason}", "Reconnect", ReconnectCommand));

    private void OnEngineLog(object? sender, string message) =>
        Dispatcher.UIThread.Post(() => State.AddNotification(message));

    private async Task ReconnectAsync()
    {
        if (_reconnecting)
        {
            return;
        }

        _reconnecting = true;
        try
        {
            State.AddNotification("Reconnecting engine...");
            await _session.ReconnectAsync();
            State.AddNotification($"Engine reconnected ({_session.EngineHello?.EngineVersion ?? "unknown version"}).");
        }
        catch (Exception error)
        {
            State.AddNotification($"Reconnect failed: {error.Message}", "Reconnect", ReconnectCommand);
        }
        finally
        {
            _reconnecting = false;
        }
    }

    private void CreateLayout()
    {
        var factory = new EditorDockFactory();
        EditorFactory = factory;
        Factory = factory;
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);
    }
}
