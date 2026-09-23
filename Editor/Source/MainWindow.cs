#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Dock.Avalonia.Controls;

namespace GameBench.Editor;

internal sealed class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TextBlock _statusText;
    private DockControl _dockControl = null!;
    private bool _closing;
    private bool _allowClose;
    private Border _chromeRoot = null!;
    private Button _maximizeButton = null!;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        Title = $"GameBench Editor - {viewModel.ProjectName}";
        Width = 1500;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        Background = EditorTheme.WindowBackground;
        TrySetWindowIcon();

        // Undecorated chrome: drop the OS title bar and caption buttons and draw our own
        // (see CreateHeader). BorderOnly keeps the native resize border so dragging edges,
        // Aero-snap and maximize-to-work-area still behave natively.
        WindowDecorations = Avalonia.Controls.WindowDecorations.BorderOnly;

        _statusText = new TextBlock
        {
            Text = "Ready",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0),
            Foreground = EditorTheme.TextMuted,
            FontSize = 12
        };
        _viewModel.StatusChanged += status => _statusText.Text = status;


        var dockControl = _dockControl = new DockControl
        {
            Factory = viewModel.Factory,
            Layout = viewModel.Layout,
            InitializeFactory = true,
            InitializeLayout = false,
            IsDockingEnabled = true
        };

        var root = new DockPanel { LastChildFill = true };

        var header = CreateHeader();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        root.Children.Add(header);

        var toolbar = CreateToolbar();
        DockPanel.SetDock(toolbar, Avalonia.Controls.Dock.Top);
        root.Children.Add(toolbar);

        root.Children.Add(new Border
        {
            Background = EditorTheme.WindowBackground,
            Padding = new Thickness(4, 4, 4, 0),
            Child = dockControl
        });

        // When maximized with an extended client area, Windows oversizes the window by the
        // resize-border thickness; pad by OffScreenMargin so nothing spills off-screen.
        _chromeRoot = new Border
        {
            Background = EditorTheme.WindowBackground,
            Child = root
        };
        Content = _chromeRoot;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            UpdateChromeForState();
    }

    private void UpdateChromeForState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (_chromeRoot != null)
            _chromeRoot.Padding = maximized ? OffScreenMargin : default;
        if (_maximizeButton != null)
        {
            _maximizeButton.Content = maximized ? RestoreGlyph() : MaximizeGlyph();
            ToolTip.SetTip(_maximizeButton, maximized ? "Restore" : "Maximize");
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            if (!_closing)
            {
                _closing = true;
                try { await _viewModel.ShutdownAsync(); }
                catch (Exception error) { System.Diagnostics.Trace.WriteLine(error); }
                _allowClose = true;
                Close();
            }
        }
        base.OnClosing(e);
    }

    private void TrySetWindowIcon()
    {
        Bitmap? logo = EditorTheme.LoadBitmap("Editor/Hazel-IconLogo-2023.png");
        if (logo != null)
            Icon = new WindowIcon(logo);
    }

    // --- Header: brand mark + menu --------------------------------------------------

    private Control CreateHeader()
    {
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
            Children =
            {
                EditorTheme.ImageIcon("Editor/Hazel-IconLogo-2023.png", EditorIcons.ContentBrowser.Cube, EditorTheme.Accent, 18),
                new TextBlock
                {
                    Text = "GAMEBENCH",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeight.Bold,
                    FontSize = 13,
                    Foreground = EditorTheme.TextBright,
                    Margin = new Thickness(0, 0, 4, 0)
                }
            }
        };
        DockPanel.SetDock(brand, Avalonia.Controls.Dock.Left);

        var menu = CreateMenu();
        DockPanel.SetDock(menu, Avalonia.Controls.Dock.Left);

        var windowControls = CreateWindowControls();
        DockPanel.SetDock(windowControls, Avalonia.Controls.Dock.Right);

        // Transparent fill that carries the move/maximize gestures for the empty title-bar
        // space between the menu and the window buttons.
        var dragSpacer = new Border { Background = Brushes.Transparent };

        var bar = new DockPanel
        {
            LastChildFill = true,
            Background = EditorTheme.TitleBar,
            Children = { brand, menu, windowControls, dragSpacer }
        };

        EnableTitleBarDrag(brand);
        EnableTitleBarDrag(dragSpacer);

        return new Border
        {
            BorderBrush = EditorTheme.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar
        };
    }

    // --- Custom window caption (min / max / close) -----------------------------------

    private Control CreateWindowControls()
    {
        var minimize = CaptionButton(MinimizeGlyph(), () => WindowState = WindowState.Minimized);
        ToolTip.SetTip(minimize, "Minimize");

        _maximizeButton = CaptionButton(MaximizeGlyph(), ToggleMaximize);
        ToolTip.SetTip(_maximizeButton, "Maximize");

        var close = CaptionButton(EditorIcons.CreatePath(EditorIcons.Common.Close, EditorTheme.Text, 12), Close, isClose: true);
        ToolTip.SetTip(close, "Close");

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { minimize, _maximizeButton, close }
        };
    }

    private Button CaptionButton(Control glyph, Action onClick, bool isClose = false)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 46,
            Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Classes = { "caption" }
        };
        if (isClose)
            button.Classes.Add("caption-close");
        button.Click += (_, _) => onClick();
        return button;
    }

    private void EnableTitleBarDrag(Control region)
    {
        region.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(region).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        region.DoubleTapped += (_, _) => ToggleMaximize();
    }

    private static Control MinimizeGlyph() => new Border
    {
        Width = 10,
        Height = 1,
        Background = EditorTheme.Text,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Control MaximizeGlyph() => new Border
    {
        Width = 10,
        Height = 10,
        BorderBrush = EditorTheme.Text,
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    // Two offset squares, matching the OS "restore down" affordance.
    private static Control RestoreGlyph()
    {
        var back = new Border
        {
            Width = 8,
            Height = 8,
            BorderBrush = EditorTheme.Text,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        var front = new Border
        {
            Width = 8,
            Height = 8,
            BorderBrush = EditorTheme.Text,
            BorderThickness = new Thickness(1),
            Background = EditorTheme.TitleBar,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        return new Grid { Width = 12, Height = 12, Children = { back, front } };
    }

    private static MenuItem MakeMenuItem(string text, string iconPath, IBrush? iconColor = null)
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                EditorIcons.CreatePath(iconPath, iconColor ?? EditorTheme.Text, 16),
                new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
            }
        };
        return new MenuItem { Header = header };
    }

    private Menu CreateMenu()
    {
        var saveScene = MakeMenuItem("Save Scene", EditorIcons.Common.Save);
        saveScene.IsEnabled = false;
        ToolTip.SetTip(saveScene, "Ctrl+S");

        var saveAll = MakeMenuItem("Save All", EditorIcons.Common.SaveAll, EditorTheme.Accent);
        saveAll.IsEnabled = false;

        var projectSettings = MakeMenuItem("Project Settings...", EditorIcons.Common.Settings);
        projectSettings.IsEnabled = false;

        var editorSettings = MakeMenuItem("Editor Settings...", EditorIcons.Common.Settings, EditorTheme.Accent);
        editorSettings.IsEnabled = false;

        var switchProject = MakeMenuItem("Switch Project...", EditorIcons.ContentBrowser.FolderOpen, EditorTheme.Accent);
        switchProject.IsEnabled = false;

        var exit = MakeMenuItem("Exit", EditorIcons.Common.Close);
        exit.Click += (_, _) => Close();

        var file = new MenuItem
        {
            Header = "_File",
            ItemsSource = new object[]
            {
                saveScene,
                saveAll,
                new Separator(),
                projectSettings,
                editorSettings,
                new Separator(),
                switchProject,
                exit
            }
        };

        var resetLayout = new MenuItem { Header = "Reset Layout" };
        resetLayout.Click += async (_, _) =>
        {
            resetLayout.IsEnabled = false;
            try
            {
                await _viewModel.ResetLayoutAsync();
                _dockControl.Factory = _viewModel.Factory;
                _dockControl.Layout = _viewModel.Layout;
            }
            finally { resetLayout.IsEnabled = true; }
        };
        var view = new MenuItem { Header = "_View", ItemsSource = new object[] { resetLayout } };

        return new Menu
        {
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new object[] { file, view }
        };
    }

    // --- Toolbar: play controls + project --------------------------------------------

    private Control CreateToolbar()
    {
        var pieButton = ActionButton("Play", "Editor/Viewport/Play.png", EditorIcons.Toolbar.Play, "success");
        var stopButton = ActionButton("Stop", "Editor/Viewport/Stop.png", EditorIcons.Toolbar.Stop, "danger");
        var runtimeButton = ActionButton("Launch", "Editor/Viewport/Simulate.png", EditorIcons.Toolbar.Play, "accent");
        var saveButton = ActionButton("Save", "Editor/Viewport/Save.png", EditorIcons.Common.Save, "accent");
        var saveAllButton = ActionButton("All", "Editor/Viewport/SaveAll.png", EditorIcons.Common.SaveAll, "accent");
        var undoButton = ActionButton("Undo", "Editor/Viewport/Undo.png", EditorIcons.Common.Undo, "accent");
        var redoButton = ActionButton("Redo", "Editor/Viewport/Redo.png", EditorIcons.Common.Redo, "accent");
        foreach (var button in new[] { pieButton, stopButton, runtimeButton, saveButton, saveAllButton, undoButton, redoButton })
        {
            SetEnabled(button, false);
            ToolTip.SetTip(button, "Available when scenes and projects are supported");
        }

        static Border Separator() => new()
        {
            Width = 1,
            Height = 22,
            Background = EditorTheme.Border,
            Margin = new Thickness(10, 0)
        };

        var projectChip = new Border
        {
            Background = EditorTheme.Surface,
            BorderBrush = EditorTheme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    EditorTheme.ImageIcon("Editor/Generic/Gear.png", EditorIcons.Common.Settings, EditorTheme.Accent, 14),
                    new TextBlock
                    {
                        Text = _viewModel.ProjectName,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 12,
                        Foreground = EditorTheme.TextBright
                    }
                }
            }
        };

        var leftGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { saveButton, saveAllButton, undoButton, redoButton, Separator(), pieButton, stopButton, Separator(), runtimeButton }
        };

        var toolbar = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(leftGroup, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(projectChip, Avalonia.Controls.Dock.Right);
        DockPanel.SetDock(_statusText, Avalonia.Controls.Dock.Right);
        toolbar.Children.Add(leftGroup);
        toolbar.Children.Add(projectChip);
        toolbar.Children.Add(_statusText);

        return new Border
        {
            Background = EditorTheme.BackgroundDark,
            Padding = new Thickness(8, 5),
            BorderBrush = EditorTheme.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = toolbar
        };
    }

    private static Button ActionButton(string label, string iconPath, string fallback, string cssClass)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    EditorTheme.ImageIcon(iconPath, fallback, Brushes.White, 14),
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
                }
            },
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        button.Classes.Add(cssClass);
        return button;
    }

    private static void SetEnabled(Button button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1.0 : 0.45;
    }

}
