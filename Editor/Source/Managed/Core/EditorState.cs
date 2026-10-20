// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GameBench.Editor.Managed.ViewModels;
using ReactiveUI;

namespace GameBench.Editor.Managed.Core;

// Shared, observable state for the whole editor. Regions bind to this instead of
// reaching into each other, mirroring how the engine hands subsystem state around
// through Application rather than through the layers themselves.
public sealed class EditorState : ViewModelBase
{
    private bool _isPlaying;

    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    // Notifications land here newest-first and render in the title-bar bell.
    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    public void AddNotification(string text, string? actionLabel = null, ICommand? action = null) =>
        Notifications.Insert(0, new NotificationItem(text, actionLabel, action));

    public void ReportStatus(string status) =>
        AddNotification($"[{DateTime.Now:HH:mm:ss}] {status}");
}
