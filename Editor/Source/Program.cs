// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using Avalonia;
using ReactiveUI.Avalonia;
using MortarEngine.Editor.Managed.Core;

namespace MortarEngine.Editor;

// The managed entry point. Unlike every other file it deliberately sits outside
// Managed/ and Native/: it is the counterpart of LaunchWindows.cpp and only knows
// how to boot the Avalonia app.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<EditorApp>()
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
