#nullable enable
using System;
using System.IO;
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

        DataTemplates.Add(new EditorDockDataTemplate());

        // Retint the Fluent accent (drives focus rings, checks, drag adorners) to the
        // editor's blue, with computed light/dark ramps so Fluent's shading stays sane.
        Resources["SystemAccentColor"] = Color.Parse("#3D82C4");
        Resources["SystemAccentColorLight1"] = Color.Parse("#4E91D0");
        Resources["SystemAccentColorLight2"] = Color.Parse("#63A1DA");
        Resources["SystemAccentColorLight3"] = Color.Parse("#82B6E5");
        Resources["SystemAccentColorDark1"] = Color.Parse("#356FA8");
        Resources["SystemAccentColorDark2"] = Color.Parse("#2C5C8C");
        Resources["SystemAccentColorDark3"] = Color.Parse("#224A72");

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
