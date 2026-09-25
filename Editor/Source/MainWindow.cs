#nullable enable
using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Dock.Avalonia.Controls;

namespace GameBench.Editor;

internal sealed class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _closing;
    private bool _allowClose;
    private Border _chromeRoot = null!;
    private Button _maximizeButton = null!;
    private DockControl _dockControl = null!;

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

        _dockControl = new DockControl
        {
            Factory = viewModel.Factory,
            Layout = viewModel.Layout,
            InitializeFactory = true,
            InitializeLayout = false,
            IsDockingEnabled = true
        };

        EditorViewportMenu.Attach(_dockControl, viewModel.EditorFactory);

        var root = new DockPanel { LastChildFill = true };

        var titleBar = CreateTitleBar();
        DockPanel.SetDock(titleBar, Avalonia.Controls.Dock.Top);
        root.Children.Add(titleBar);

        var mainToolbar = CreateMainToolbar();
        DockPanel.SetDock(mainToolbar, Avalonia.Controls.Dock.Top);
        root.Children.Add(mainToolbar);

        root.Children.Add(new Border
        {
            Background = EditorTheme.WindowBackground,
            Padding = new Thickness(4, 4, 4, 0),
            Child = _dockControl
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

    // --- Title bar: brand, menubar, notifications and caption -----------------------

    // Hazelnut's UI_DrawTitlebar: a 57px bar filled with titlebar, overlaid by a
    // 380px-wide horizontal gradient from the running-state colour back to titlebar.
    private const double TitleBarHeight = 57.0;
    private const double TitleBarGradientWidth = 380.0;

    private Border _stateGradient = null!;

    private Control CreateTitleBar()
    {
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 8, 0),
            Children =
            {
                CreateBrandMark(),
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

        // Stands in for the old "Ready" status label: status messages collect here as a
        // log behind the bell instead of overwriting a single line of text.
        var notifications = CreateNotificationsButton();
        DockPanel.SetDock(notifications, Avalonia.Controls.Dock.Right);

        // Transparent fill that carries the move/maximize gestures for the empty
        // title-bar space between the menu and the action row.
        var dragSpacer = new Border { Background = Brushes.Transparent };

        var content = new DockPanel
        {
            LastChildFill = true,
            Children = { brand, menu, windowControls, notifications, dragSpacer }
        };

        EnableTitleBarDrag(brand);
        EnableTitleBarDrag(dragSpacer);

        // Behind the content and hit-test transparent so it never eats a click.
        _stateGradient = new Border
        {
            Width = TitleBarGradientWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
            Background = TitleBarStateGradient(EditorTheme.TitleBarStopColor),
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(150),
                    Easing = new LinearEasing()
                }
            }
        };

        return new Border
        {
            Height = TitleBarHeight,
            Background = EditorTheme.TitleBar,
            BorderBrush = EditorTheme.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new Panel { Children = { _stateGradient, content } }
        };
    }

    private static Control CreateBrandMark()
    {
        Bitmap? logo = EditorTheme.LoadBitmap("Editor/HazelLogo_Light.png");
        if (logo == null)
            return EditorTheme.ImageIcon("Editor/HazelLogo_Light.png", "", EditorTheme.Accent, 24);

        return new Image
        {
            Source = logo,
            Width = 38,
            Height = 45,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static IBrush TitleBarStateGradient(Color state) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(state, 0.0),
            new GradientStop(EditorTheme.TitleBarColor, 1.0)
        }
    };

    private void SetTitleBarState(bool playing)
    {
        _stateGradient.Background = TitleBarStateGradient(
            playing ? EditorTheme.TitleBarPlayColor : EditorTheme.TitleBarStopColor);
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
            Height = TitleBarHeight,
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

    private Menu CreateMenu()
    {
        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Close();

        // No icon: the viewport entry lives as plain text, unlike the titlebar's old
        // monitor button which was dropped when it moved into this dropdown.
        var viewport = new MenuItem { Header = "_Viewport" };
        viewport.Click += (_, _) => _viewModel.ShowViewport();

        return new Menu
        {
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new object[]
            {
                new MenuItem { Header = "_File", ItemsSource = new object[] { exit } },
                new MenuItem { Header = "_Window", ItemsSource = new object[] { viewport } }
            }
        };
    }

    // --- Main toolbar: transport on its own row under the title bar -----------------

    private const double MainToolbarHeight = 44.0;

    private Control CreateMainToolbar() => new Border
    {
        Height = MainToolbarHeight,
        Background = EditorTheme.WindowBackground,
        BorderBrush = EditorTheme.BorderSubtle,
        BorderThickness = new Thickness(0, 0, 0, 1),
        Padding = new Thickness(12, 0),
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Children = { CreatePlayToggleButton() }
        }
    };

    // --- Transport: play / stop toggle -----------------------------------------------

    private bool _isPlaying;

    private Button CreatePlayToggleButton()
    {
        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Classes = { "toolbar" }
        };

        void Render()
        {
            button.Content = _isPlaying
                ? ToolbarContent(
                    EditorTheme.TintedIcon("Editor/Viewport/Stop.png", EditorIcons.Toolbar.Stop, EditorTheme.TextColor, 14),
                    "Stop")
                : ToolbarContent(
                    EditorTheme.TintedIcon("Editor/Viewport/Play.png", EditorIcons.Toolbar.Play, EditorTheme.TextColor, 14),
                    "Play");
            ToolTip.SetTip(button, _isPlaying ? "Stop" : "Play the game scene");
        }

        Render();
        button.Click += (_, _) =>
        {
            _isPlaying = !_isPlaying;
            SetTitleBarState(_isPlaying);
            Render();
        };
        return button;
    }

    private static StackPanel ToolbarContent(Control icon, string label) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            icon,
            new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
        }
    };

    // --- Notifications ----------------------------------------------------------------

    private Button CreateNotificationsButton()
    {
        var count = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = EditorTheme.Accent,
            IsVisible = false
        };

        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Classes = { "toolbar" },
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { EditorIcons.CreatePath(EditorIcons.Notifications.Bell, EditorTheme.Text, 14), count }
            }
        };
        ToolTip.SetTip(button, "Notifications");

        var empty = new TextBlock
        {
            Text = "No notifications",
            FontSize = 12,
            Foreground = EditorTheme.TextMuted
        };

        var list = new ItemsControl
        {
            ItemsSource = _viewModel.Notifications,
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 1)
            })
        };

        var scroll = new ScrollViewer { Content = list, MaxHeight = 320 };

        var clear = new Button { Classes = { "toolbar" }, Content = "Clear" };
        clear.Click += (_, _) => _viewModel.Notifications.Clear();

        void Refresh()
        {
            int total = _viewModel.Notifications.Count;
            count.Text = total > 0 ? total.ToString() : string.Empty;
            count.IsVisible = total > 0;
            empty.IsVisible = total == 0;
            scroll.IsVisible = total > 0;
            clear.IsVisible = total > 0;
        }

        _viewModel.Notifications.CollectionChanged += (_, _) => Refresh();
        Refresh();

        var flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            Content = new StackPanel
            {
                Width = 320,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Notifications",
                        FontSize = 11,
                        FontWeight = FontWeight.Bold,
                        Foreground = EditorTheme.TextMuted
                    },
                    empty,
                    scroll,
                    clear
                }
            }
        };
        button.Flyout = flyout;
        return button;
    }

}
