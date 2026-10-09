using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;

namespace AvaloniaSplashScreenDemo.ViewModels
{
    public class SplashScreenViewModel : ViewModelBase
    {
        public string StartupMessage {
            get => _startupMessage;
            set { this.RaiseAndSetIfChanged(ref _startupMessage, value); }
        }
        private string _startupMessage = "Starting application...";

        public ICommand CancelCommand { get; }

        public SplashScreenViewModel()
        {
            CancelCommand = ReactiveCommand.Create(Cancel);
        }

        public void Cancel()
        {
            StartupMessage = "Cancelling...";
            _cts.Cancel();
        }

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public CancellationToken CancellationToken => _cts.Token;
    }
}