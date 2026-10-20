// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Windows.Input;

namespace GameBench.Editor.Managed.Core;

// One entry of the title-bar notifications bell. Text-only entries come from
// ReportStatus; entries with an ActionLabel render a small action button
// (e.g. "Reconnect" when the engine disconnects).
public sealed class NotificationItem
{
    public NotificationItem(string text, string? actionLabel = null, ICommand? action = null)
    {
        Text = text;
        ActionLabel = actionLabel;
        Action = action;
    }

    public string Text { get; }

    public string? ActionLabel { get; }

    public ICommand? Action { get; }

    public bool HasAction => Action is not null;
}
