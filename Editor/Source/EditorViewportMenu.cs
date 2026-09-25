#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;

namespace GameBench.Editor;

internal static class EditorViewportMenu
{
    public static void Attach(DockControl control, EditorDockFactory factory)
    {
        control.AddHandler(
            InputElement.ContextRequestedEvent,
            (_, e) => OnContextRequested(factory, e),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private static void OnContextRequested(EditorDockFactory factory, ContextRequestedEventArgs e)
    {
        if (e.Source is not Visual source)
            return;

        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is not DocumentTabStripItem tab)
                continue;
            if (tab.DataContext is not ViewportPanelViewModel)
                return;

            e.Handled = true;
            Open(tab, factory);
            return;
        }
    }

    private static void Open(Control target, EditorDockFactory factory)
    {
        var placement = factory.ViewportPlacement;

        var dock = MenuEntry("Dock");
        dock.IsEnabled = placement != ViewportPlacement.Docked;
        dock.Click += (_, _) => factory.ShowViewport();

        var floating = MenuEntry("Float");
        floating.IsEnabled = placement == ViewportPlacement.Docked;
        floating.Click += (_, _) => factory.FloatViewport();

        var close = MenuEntry("Close", EditorIcons.Common.Close);
        close.IsEnabled = placement != ViewportPlacement.Hidden;
        close.Click += (_, _) => factory.HideViewport();

        new ContextMenu { ItemsSource = new object[] { dock, floating, close } }.Open(target);
    }

    private static MenuItem MenuEntry(string label, string? icon = null)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (icon is not null)
        {
            row.Children.Add(new Path
            {
                Data = StreamGeometry.Parse(icon),
                Fill = EditorTheme.TextMuted,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        return new MenuItem { Header = row };
    }
}
