using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AvaloniaSplashScreenDemo.Views
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            TrySetLogo();
        }

        private void TrySetLogo()
        {
            try
            {
                // Matches EditorTheme.LoadBitmap's convention: bin/Resources/<relative path>.
                string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Editor", "HazelLogo_Light.png");
                if (File.Exists(path))
                {
                    LogoImage.Source = new Bitmap(path);
                    LogoImage.IsVisible = true;
                }
            }
            catch
            {
                // ICO decoding is not guaranteed on every Skia build; hide the logo rather
                // than fail startup.
                LogoImage.IsVisible = false;
            }
        }
    }
}