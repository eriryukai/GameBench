// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Dock.Avalonia.Themes;
using Dock.Avalonia.Themes.Fluent;
using GameBench.Editor.Managed.Styling;
using GameBench.Editor.Managed.UI;
using GameBench.Editor.Managed.UI.SplashScreen.ViewModels;
using SplashScreenView = GameBench.Editor.Managed.UI.SplashScreen.Views.SplashScreen;

namespace GameBench.Editor.Managed.Core;

// The editor's composition root, the managed counterpart of Interface::Application:
// it owns the lifetime, wires the styling, and hands over to the editor window once
// the splash screen reports the workspace is ready.
internal sealed class EditorApp : Application
{
    public override void Initialize()
    {
        PreInitialize();
        OnInitialize();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            PostInitialize(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void PreInitialize()
    {
        SetupStyles();
        RegisterThemeResources();
    }

    private void OnInitialize()
    {
    }

    private async void PostInitialize(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var splashViewModel = new SplashScreenViewModel();
        var splashScreen = new SplashScreenView
        {
            DataContext = splashViewModel
        };
        desktop.MainWindow = splashScreen;
        splashScreen.Show();

        // Run startup work behind the splash screen, then build the real editor window.
        MainWindow? mainWindow = null;
        try
        {
            mainWindow = await CreateMainWindowAsync(splashViewModel);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.WriteLine(error);
        }

        if (mainWindow == null || splashViewModel.CancellationToken.IsCancellationRequested)
        {
            // Startup was cancelled (or failed) before the editor was ready: drop the splash
            // and shut the application down instead of opening the editor anyway.
            splashScreen.Close();
            desktop.Shutdown();
            return;
        }

        desktop.MainWindow = mainWindow;
        splashScreen.Close();
        mainWindow.Show();
    }

    private static async Task<MainWindow?> CreateMainWindowAsync(SplashScreenViewModel splashViewModel)
    {
        string[] steps = { "Loading engine...", "Preparing workspace...", "Starting editor..." };
        for (int i = 0; i < steps.Length; i++)
        {
            if (splashViewModel.CancellationToken.IsCancellationRequested)
                return null;

            splashViewModel.StartupMessage = steps[i];
            try
            {
                //@TODO: This is where the IPC connection and server should spins up. Once we have a valid client connection,
                //       shuts down the SplashScreen
                await Task.Delay(TimeSpan.FromSeconds(2), splashViewModel.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return new MainWindow();
    }

    private void SetupStyles()
    {
        RequestedThemeVariant = ThemeVariant.Dark;

        Resources["SystemAccentColor"] = Color.Parse("#EC9E24");
        Resources["SystemAccentColorLight1"] = Color.Parse("#EF8948");
        Resources["SystemAccentColorLight2"] = Color.Parse("#F2A16E");
        Resources["SystemAccentColorLight3"] = Color.Parse("#F6BA93");
        Resources["SystemAccentColorDark1"] = Color.Parse("#D35E12");
        Resources["SystemAccentColorDark2"] = Color.Parse("#AE4D0F");
        Resources["SystemAccentColorDark3"] = Color.Parse("#883C0C");

        Resources["ScrollBarBackground"] = EditorTheme.ScrollTrack;
        Resources["ScrollBarBackgroundPointerOver"] = EditorTheme.ScrollTrack;
        Resources["ScrollBarTrackFill"] = Brushes.Transparent;
        Resources["ScrollBarTrackFillPointerOver"] = Brushes.Transparent;
        Resources["ScrollBarPanningThumbBackground"] = EditorTheme.ScrollThumb;
        Resources["ScrollBarThumbBackgroundColor"] = EditorTheme.ScrollThumbHover;
        Resources["ScrollBarThumbFillPointerOver"] = EditorTheme.ScrollThumbHover;
        Resources["ScrollBarThumbFillPressed"] = EditorTheme.ScrollThumbActive;

        Styles.Add(new FluentTheme());
        Styles.Add(new DockFluentTheme
        {
            DensityStyle = DockDensityStyle.Compact,
            CacheDocumentTabContent = true
        });
        Styles.Add(new EditorStyles());
    }

    // Brushes the XAML views reference by key. They mirror EditorTheme so markup does not
    // have to hard-code colours that already live in the C++-derived theme.
    private void RegisterThemeResources()
    {
        Resources["EditorWindowBackground"] = EditorTheme.WindowBackground;
        Resources["EditorTitleBar"] = EditorTheme.TitleBar;
        Resources["EditorBorderSubtle"] = EditorTheme.BorderSubtle;
        Resources["EditorText"] = EditorTheme.Text;
        Resources["EditorTextBright"] = EditorTheme.TextBright;
        Resources["EditorTextMuted"] = EditorTheme.TextMuted;
        Resources["EditorAccent"] = EditorTheme.Accent;
    }
}
