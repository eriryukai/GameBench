// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MortarEngine.Editor.Managed.Core;
using MortarEngine.Editor.Managed.Styling;
using MortarEngine.Editor.Managed.ViewModels;
using ReactiveUI;

namespace MortarEngine.Editor.Managed.UI.Toolbar;

// The play / stop transport. It flips EditorState.IsPlaying and reflects the result back
// as the button label, tooltip and icon, so the title bar can react to the same state.
public sealed class TransportViewModel : ViewModelBase
{
    private readonly EditorState _state;

    public TransportViewModel(EditorState state)
    {
        _state = state;
        ToggleCommand = ReactiveCommand.Create(() => state.IsPlaying = !state.IsPlaying);

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorState.IsPlaying))
                Refresh();
        };
    }

    public ICommand ToggleCommand { get; }

    public bool IsPlaying => _state.IsPlaying;

    public string Label => _state.IsPlaying ? "Stop" : "Play";

    public string ToolTip => _state.IsPlaying ? "Stop" : "Play the game scene";

    public Bitmap? Icon => EditorTheme.TintedBitmap(
        _state.IsPlaying ? "Editor/Viewport/Stop.png" : "Editor/Viewport/Play.png",
        EditorTheme.TextColor);

    public Geometry IconPath => StreamGeometry.Parse(
        _state.IsPlaying ? EditorIcons.Toolbar.Stop : EditorIcons.Toolbar.Play);

    private void Refresh()
    {
        this.RaisePropertyChanged(nameof(IsPlaying));
        this.RaisePropertyChanged(nameof(Label));
        this.RaisePropertyChanged(nameof(ToolTip));
        this.RaisePropertyChanged(nameof(Icon));
        this.RaisePropertyChanged(nameof(IconPath));
    }
}
