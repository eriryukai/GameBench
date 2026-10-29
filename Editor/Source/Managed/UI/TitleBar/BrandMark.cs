// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MortarEngine.Editor.Managed.Styling;

namespace MortarEngine.Editor.Managed.UI.TitleBar;

// The editor brand mark. Presentation-only, so it is a tiny reusable control rather than a
// view model; it just loads the shipped logo and falls back to a tinted glyph if it is missing.
public sealed class BrandMark : UserControl
{
    public BrandMark()
    {
        Bitmap? logo = EditorTheme.LoadBitmap("Editor/MortarEngine_Logo.png");
        if (logo == null)
        {
            Content = EditorTheme.ImageIcon("Editor/MortarEngine_Logo.png", "", EditorTheme.Accent, 24);
            return;
        }

        Content = new Image
        {
            Source = logo,
            Width = 36,
            Height = 36,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
