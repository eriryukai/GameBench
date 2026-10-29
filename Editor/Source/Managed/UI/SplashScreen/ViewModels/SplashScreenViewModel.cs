// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Threading;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using MortarEngine.Editor.Managed.Styling;
using MortarEngine.Editor.Managed.ViewModels;
using ReactiveUI;

namespace MortarEngine.Editor.Managed.UI.SplashScreen.ViewModels;

public class SplashScreenViewModel : ViewModelBase
{
    private string _startupMessage = "Starting application...";

    // The mark asset is a white silhouette with real alpha; tinting it with the brand red
    // gives the splash lockup its mark without shipping a second bitmap.
    public Bitmap? Mark { get; } =
        EditorTheme.TintedBitmap("Editor/MortarEngine_Logo.png", EditorTheme.BrandRed);

    public ICommand CancelCommand { get; }

    public SplashScreenViewModel()
    {
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public string StartupMessage
    {
        get => _startupMessage;
        set => this.RaiseAndSetIfChanged(ref _startupMessage, value);
    }

    public CancellationToken CancellationToken => _cts.Token;

    private void Cancel()
    {
        StartupMessage = "Cancelling...";
        _cts.Cancel();
    }

    private readonly CancellationTokenSource _cts = new();
}
