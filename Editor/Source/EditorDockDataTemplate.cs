#nullable enable
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace GameBench.Editor;

internal sealed class EditorDockDataTemplate : IDataTemplate
{
    public Control? Build(object? data) => data switch
    {
        ViewportPanelViewModel viewport => EditorPanelViews.CreateViewport(viewport),
        SceneHierarchyPanelViewModel => EditorPanelViews.EmptyPanel("No scene loaded", "Search actors..."),
        DetailsPanelViewModel => EditorPanelViews.EmptyPanel("Nothing selected"),
        ContentBrowserPanelViewModel => EditorPanelViews.EmptyPanel("No project content", "Search content..."),
        ProfilerPanelViewModel => EditorPanelViews.EmptyPanel("Profiler is not connected"),
        _ => null
    };

    public bool Match(object? data) => data is ViewportPanelViewModel or SceneHierarchyPanelViewModel
        or DetailsPanelViewModel or ContentBrowserPanelViewModel or ProfilerPanelViewModel;
}

internal static class EditorPanelViews
{
    public static Control CreateViewport(ViewportPanelViewModel viewModel)
    {
        var engine = new EngineViewportControl(viewModel);
        return new Grid { Children = { engine, CreateViewportOverlay() } };
    }

    public static Control EmptyPanel(string message, string? searchHint = null)
    {
        var panel = new DockPanel { Background = EditorTheme.Background };
        if (searchHint != null)
        {
            var search = new TextBox { PlaceholderText = searchHint, IsEnabled = false, Margin = new Thickness(8) };
            DockPanel.SetDock(search, Avalonia.Controls.Dock.Top);
            panel.Children.Add(search);
        }
        panel.Children.Add(new TextBlock
        {
            Text = message, Foreground = EditorTheme.TextMuted, FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static Control CreateViewportOverlay()
    {
        var tools = new (string Label, string Icon, string Fallback)[]
        {
            ("Select (Q)", "Editor/Generic/Pointer.png", EditorIcons.SceneHierarchy.Actor),
            ("Move (W)", "Editor/Viewport/MoveTool.png", EditorIcons.SceneHierarchy.Actor),
            ("Rotate (E)", "Editor/Viewport/RotateTool.png", EditorIcons.SceneHierarchy.Actor),
            ("Scale (R)", "Editor/Viewport/ScaleTool.png", EditorIcons.SceneHierarchy.Actor),
        };

        var buttons = new List<Button>();
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };

        void SetActive(Button active)
        {
            foreach (Button b in buttons)
                b.Classes.Remove("active");
            active.Classes.Add("active");
        }

        foreach (var (label, icon, fallback) in tools)
        {
            var button = new Button
            {
                IsEnabled = false,
                Classes = { "toolbar" },
                Padding = new Thickness(6, 5),
                Content = EditorTheme.ImageIcon(icon, fallback, EditorTheme.Text, 16)
            };
            ToolTip.SetTip(button, label + " — available when scenes are supported");
            buttons.Add(button);
            button.Click += (_, _) => SetActive(button);
            panel.Children.Add(button);
        }
        SetActive(buttons[0]);

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8),
            Padding = new Thickness(3),
            Background = Brush.Parse("#E61C1C20"),
            BorderBrush = EditorTheme.BorderStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = panel
        };
    }

}


