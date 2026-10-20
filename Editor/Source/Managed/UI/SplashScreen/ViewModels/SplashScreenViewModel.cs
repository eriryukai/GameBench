// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Threading;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using GameBench.Editor.Managed.Styling;
using GameBench.Editor.Managed.ViewModels;
using ReactiveUI;

namespace GameBench.Editor.Managed.UI.SplashScreen.ViewModels;

public class SplashScreenViewModel : ViewModelBase
{
    private string _startupMessage = "Starting application...";
    private bool _isError;
    private string _errorMessage = string.Empty;

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

    // Startup failed: the splash stays up (showing ErrorMessage) until the user quits.
    public bool IsError
    {
        get => _isError;
        set
        {
            this.RaiseAndSetIfChanged(ref _isError, value);
            this.RaisePropertyChanged(nameof(CancelLabel));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string CancelLabel => _isError ? "Quit" : "Cancel";

    public CancellationToken CancellationToken => _cts.Token;

    private void Cancel()
    {
        StartupMessage = _isError ? "Closing..." : "Cancelling...";
        _cts.Cancel();
    }

    private readonly CancellationTokenSource _cts = new();
}
