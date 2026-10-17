// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Windows.Input;
using Avalonia.Media;
using GameBench.Editor.Managed.Styling;
using GameBench.Editor.Managed.ViewModels;
using ReactiveUI;

namespace GameBench.Editor.Managed.UI.Caption;

// Drives the custom min / max / close buttons. Window actions are injected by the shell
// (MainWindow), so this view model never has to know about the window itself.
public sealed class WindowCaptionViewModel : ViewModelBase
{
    private bool _isMaximized;

    public WindowCaptionViewModel(Action minimize, Action toggleMaximize, Action close)
    {
        MinimizeCommand = ReactiveCommand.Create(minimize);
        ToggleMaximizeCommand = ReactiveCommand.Create(toggleMaximize);
        CloseCommand = ReactiveCommand.Create(close);
    }

    public ICommand MinimizeCommand { get; }
    public ICommand ToggleMaximizeCommand { get; }
    public ICommand CloseCommand { get; }

    public Geometry CloseIcon => StreamGeometry.Parse(EditorIcons.Common.Close);

    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            this.RaiseAndSetIfChanged(ref _isMaximized, value);
            this.RaisePropertyChanged(nameof(MaximizeToolTip));
        }
    }

    public string MaximizeToolTip => _isMaximized ? "Restore" : "Maximize";
}
