// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using MortarEngine.Editor.Managed.Core;
using MortarEngine.Editor.Managed.UI.Caption;
using MortarEngine.Editor.Managed.UI.Notifications;
using MortarEngine.Editor.Managed.ViewModels;

namespace MortarEngine.Editor.Managed.UI.TitleBar;

// Owns the title-bar lockup: the running-state gradient (through EditorState), the menu,
// the notifications bell and the window caption buttons.
public sealed class TitleBarViewModel : ViewModelBase
{
    public TitleBarViewModel(EditorState state, WindowCaptionViewModel caption)
    {
        State = state;
        Caption = caption;
        Notifications = new NotificationsViewModel(state);
    }

    public EditorState State { get; }
    public WindowCaptionViewModel Caption { get; }
    public NotificationsViewModel Notifications { get; }
}
