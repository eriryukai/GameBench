// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using GameBench.Editor.Managed.Styling;
using GameBench.Editor.Managed.UI.Caption;

namespace GameBench.Editor.Managed.UI;

// The editor shell. It wires the window-level behaviour (chrome, closing, icon) and composes
// the region views; everything the shell used to build inline now lives in its own region.
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private Border? _chromeRoot;
    private bool _closing;
    private bool _allowClose;

    public MainWindow()
    {
        var caption = new WindowCaptionViewModel(
            minimize: () => WindowState = WindowState.Minimized,
            toggleMaximize: ToggleMaximize,
            close: Close);
        _viewModel = new MainWindowViewModel(caption);
        DataContext = _viewModel;

        InitializeComponent();

        _chromeRoot = this.FindControl<Border>("ChromeRoot");
        TrySetWindowIcon();
        UpdateChromeForState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            UpdateChromeForState();
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

    private void UpdateChromeForState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (_chromeRoot != null)
            _chromeRoot.Padding = maximized ? OffScreenMargin : default;
        _viewModel.TitleBar.Caption.IsMaximized = maximized;
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TrySetWindowIcon()
    {
        Bitmap? logo = EditorTheme.LoadBitmap("Editor/MortarEngine_IconAppLogo.png");
        if (logo != null)
            Icon = new WindowIcon(logo);
    }
}
