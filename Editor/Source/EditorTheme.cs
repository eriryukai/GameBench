#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GameBench.Editor;

// Central palette + asset helpers for the editor. Tuned to a Hazel/Unreal-style dark
// theme: neutral graphite surfaces, a warm gold selection (Hazel) and a blue primary
// accent (Unreal). Brush names are kept stable so existing panels keep compiling.
internal static class EditorTheme
{
    // --- Surfaces (dark -> light layering) ---
    public static readonly IBrush WindowBackground = Brush.Parse("#16161A"); // app/window chrome
    public static readonly IBrush TitleBar = Brush.Parse("#121214");
    public static readonly IBrush BackgroundDark = Brush.Parse("#1A1A1E"); // toolbars, panel headers
    public static readonly IBrush Background = Brush.Parse("#202024");      // panel body
    public static readonly IBrush Surface = Brush.Parse("#26262C");         // elevated sections/cards
    public static readonly IBrush SurfaceAlt = Brush.Parse("#2C2C33");
    public static readonly IBrush GroupHeader = Brush.Parse("#2E2E35");
    public static readonly IBrush PropertyField = Brush.Parse("#141417");   // input fields
    public static readonly IBrush Hover = Brush.Parse("#34343C");
    public static readonly IBrush Pressed = Brush.Parse("#3D3D46");

    // --- Lines ---
    public static readonly IBrush Border = Brush.Parse("#34343B");
    public static readonly IBrush BorderStrong = Brush.Parse("#42424A");
    public static readonly IBrush BorderSubtle = Brush.Parse("#2A2A30");

    // --- Text ---
    public static readonly IBrush Text = Brush.Parse("#D4D4D9");
    public static readonly IBrush TextBright = Brush.Parse("#F1F1F4");
    public static readonly IBrush TextMuted = Brush.Parse("#8C8C93");
    public static readonly IBrush TextDim = Brush.Parse("#6B6B72");

    // --- Accents ---
    public static readonly IBrush Selection = Brush.Parse("#EDC077");      // Hazel warm gold (selection text)
    public static readonly IBrush SelectionRow = Brush.Parse("#3A3022");   // selected row fill (warm)
    public static readonly IBrush SelectionRowHover = Brush.Parse("#312A20");
    public static readonly IBrush Accent = Brush.Parse("#3D82C4");         // primary blue (buttons/links)
    public static readonly IBrush AccentHover = Brush.Parse("#4A93D8");
    public static readonly IBrush AccentPressed = Brush.Parse("#346FA8");
    public static readonly IBrush Blue = Brush.Parse("#2D5478");
    public static readonly IBrush Red = Brush.Parse("#5C1E1E");

    // --- Toolbar action buttons (Play / Stop / Runtime) ---
    public static readonly IBrush PlayGreen = Brush.Parse("#1E8E3E");
    public static readonly IBrush PlayGreenHover = Brush.Parse("#27A249");
    public static readonly IBrush StopRed = Brush.Parse("#C2433A");
    public static readonly IBrush StopRedHover = Brush.Parse("#D5534A");
    // Kept for back-compat with older call sites.
    public static readonly IBrush TitleBarGreen = Brush.Parse("#1E8E3E");

    // --- Colors (for places that need Color, not Brush) ---
    public static readonly Color AccentColor = Color.Parse("#3D82C4");
    public static readonly Color SelectionColor = Color.Parse("#EDC077");

    // UI font: Inter is registered via WithInterFont() and reads as the default; named
    // here so headers/labels can opt in explicitly.
    public static readonly FontFamily UiFont = ResolveUiFont();

    private static FontFamily ResolveUiFont()
    {
        // Prefer the exported Open Sans if it has been embedded (avares); otherwise fall
        // back to Inter (the app default). Construction never throws on a missing family
        // - Avalonia resolves it lazily and falls back - so this is safe either way.
        try
        {
            return new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter");
        }
        catch
        {
            return FontFamily.Default;
        }
    }

    private static readonly Dictionary<string, Bitmap?> s_Bitmaps = new(StringComparer.OrdinalIgnoreCase);

    public static Control ImageIcon(string relativePath, string fallbackPathData, IBrush? fallbackBrush = null, double size = 16.0)
    {
        Bitmap? bitmap = LoadBitmap(relativePath);
        if (bitmap == null)
            return EditorIcons.CreatePath(fallbackPathData, fallbackBrush ?? TextBright, size);

        return new Image
        {
            Source = bitmap,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0)
        };
    }

    public static Bitmap? LoadBitmap(string relativePath)
    {
        if (s_Bitmaps.TryGetValue(relativePath, out Bitmap? bitmap))
            return bitmap;

        string path = Path.Combine(AppContext.BaseDirectory, "Resources", relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            s_Bitmaps[relativePath] = null;
            return null;
        }

        try
        {
            bitmap = new Bitmap(path);
            s_Bitmaps[relativePath] = bitmap;
            return bitmap;
        }
        catch
        {
            s_Bitmaps[relativePath] = null;
            return null;
        }
    }
}
