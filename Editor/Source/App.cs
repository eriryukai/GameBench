#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Dock.Avalonia.Themes;
using Dock.Avalonia.Themes.Fluent;

namespace GameBench.Editor;

internal sealed class App : Application
{
    public override void Initialize()
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

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new MainWindow(new MainWindowViewModel());
        }

        base.OnFrameworkInitializationCompleted();
    }

}
