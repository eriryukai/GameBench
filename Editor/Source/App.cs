#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using GameBench.Editor.SplashScreen.ViewModels;
using SplashScreenView = GameBench.Editor.SplashScreen.Views.SplashScreen;
using Dock.Avalonia.Themes;
using Dock.Avalonia.Themes.Fluent;
using System;
using System.Threading.Tasks;

namespace GameBench.Editor;

internal sealed class App : Application
{
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
    public void PreInitialize()
    {
        SetupStyles();
    }
    public void OnInitialize()
    {

    }

    public async void PostInitialize(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var splashVM = new SplashScreenViewModel();
        var splashScreen = new SplashScreenView
        {
            DataContext = splashVM
        };
        desktop.MainWindow = splashScreen;
        splashScreen.Show();

        // Run startup work behind the splash screen, then build the real editor window.
        MainWindow? mainWindow = null;
        try
        {
            mainWindow = await CreateMainWindowAsync(splashVM);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.WriteLine(error);
        }

        if (mainWindow == null || splashVM.CancellationToken.IsCancellationRequested)
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

    private static async Task<MainWindow?> CreateMainWindowAsync(SplashScreenViewModel splashVM)
    {
        string[] steps = { "Loading engine...", "Preparing workspace...", "Starting editor..." };
        for (int i = 0; i < steps.Length; i++)
        {
            if (splashVM.CancellationToken.IsCancellationRequested)
                return null;

            splashVM.StartupMessage = steps[i];
            try
            {
                //@TODO: This is where the IPC connection and server should spins up. Once we have a valid client connection,
                //       shuts down the SplashScreen
                await Task.Delay(TimeSpan.FromSeconds(2), splashVM.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return new MainWindow(new MainWindowViewModel());
    }
}