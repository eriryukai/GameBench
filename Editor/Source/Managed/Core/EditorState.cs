// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Collections.ObjectModel;
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

    // Status messages land here newest-first and render in the title-bar notifications bell.
    public ObservableCollection<string> Notifications { get; } = new();

    public void ReportStatus(string status) =>
        Notifications.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {status}");
}
