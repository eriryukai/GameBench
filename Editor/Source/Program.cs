#nullable enable
using System;
using Avalonia;
using ReactiveUI.Avalonia;
namespace GameBench.Editor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.AngleEgl }
            })
            .WithInterFont()
            .UsePlatformDetect()
            .UseReactiveUI(_ => { })
            .LogToTrace();
    }
}
