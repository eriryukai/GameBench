// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia.Media;
using GameBench.Editor.Managed.Core;
using GameBench.Editor.Managed.Styling;
using GameBench.Editor.Managed.ViewModels;
using ReactiveUI;
namespace GameBench.Editor.Managed.UI.Notifications;

// Renders the title-bar notifications bell: the unread count, the flyout log and the
// clear action. Backed by EditorState.Notifications so any region can report status.
public sealed class NotificationsViewModel : ViewModelBase
{
    private readonly EditorState _state;

    public NotificationsViewModel(EditorState state)
    {
        _state = state;
        ClearCommand = ReactiveCommand.Create(() => state.Notifications.Clear());
        state.Notifications.CollectionChanged += OnNotificationsChanged;
    }

    public ObservableCollection<NotificationItem> Items => _state.Notifications;

    public bool HasNotifications => _state.Notifications.Count > 0;

    public string CountText =>
        _state.Notifications.Count > 0 ? _state.Notifications.Count.ToString() : string.Empty;

    public Geometry BellIcon => StreamGeometry.Parse(EditorIcons.Notifications.Bell);

    public ICommand ClearCommand { get; }

    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(HasNotifications));
        this.RaisePropertyChanged(nameof(CountText));
    }
}
