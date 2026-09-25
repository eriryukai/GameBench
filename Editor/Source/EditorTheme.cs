#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GameBench.Editor;
internal static class EditorTheme
{
    // --- Surfaces (dark -> light layering) ---
    // WindowBg is forced to 0.15 in VulkanImGuiLayer.cpp after SetDarkThemeV2Colors runs.
    public static readonly IBrush WindowBackground = Brush.Parse("#262626"); // app/window chrome
    public static readonly IBrush TitleBar = Brush.Parse("#151515");         // Colors::Theme::titlebar
    public static readonly IBrush BackgroundDark = Brush.Parse("#1A1A1A");   // backgroundDark
    public static readonly IBrush Surface = Brush.Parse("#323232");          // backgroundPopup
    public static readonly IBrush GroupHeader = Brush.Parse("#2F2F2F");      // groupHeader
    public static readonly IBrush PropertyField = Brush.Parse("#0F0F0F");    // propertyField
    public static readonly IBrush Hover = Brush.Parse("#464646");            // ImGuiCol_ButtonHovered
    public static readonly IBrush Pressed = Brush.Parse("#313131");          // ButtonActive @150 over WindowBg

    // --- Lines ---
    public static readonly IBrush Border = Brush.Parse("#1A1A1A");           // ImGuiCol_Border = backgroundDark
    public static readonly IBrush BorderStrong = Brush.Parse("#2F2F2F");
    public static readonly IBrush BorderSubtle = Brush.Parse("#0A0A0A");
    public static readonly IBrush Splitter = Brush.Parse("#2F2F2F");

    // --- Text ---
    public static readonly IBrush Text = Brush.Parse("#C0C0C0");             // text
    public static readonly IBrush TextBright = Brush.Parse("#D2D2D2");       // textBrighter
    public static readonly IBrush TextMuted = Brush.Parse("#808080");        // textDarker

    // --- Accents ---
    public static readonly IBrush Accent = Brush.Parse("#EC9E24");           // accent
    public static readonly IBrush AccentHover = Brush.Parse("#EF8948");      // accent L+8
    public static readonly IBrush AccentPressed = Brush.Parse("#D35E12");    // accent L-8

    // Selected rows are filled solid gold with dark text, exactly like ContentBrowserPanel.
    public static readonly IBrush SelectionRow = Brush.Parse("#EDC077");
    public static readonly IBrush SelectionRowHover = Brush.Parse("#D9AE72");

    // --- Titlebar state colours (EditorLayer::UI_DrawTitlebar) ---
    public static readonly Color TitleBarColor = Color.Parse("#151515");
    public static readonly Color TitleBarPlayColor = Color.Parse("#BA421E");
    public static readonly Color TitleBarStopColor = Color.Parse("#12581E");

    // --- Derived control colours from SetDarkThemeV2Colors ---
    public static readonly IBrush ButtonFace = Brush.Parse("#C8383838");        // ImColor(56,56,56,200)
    public static readonly IBrush TabHover = Brush.Parse("#1EFFE187");          // ImColor(255,225,135,30)
    public static readonly IBrush TabActive = Brush.Parse("#3CFFE187");         // ImColor(255,225,135,60)
    public static readonly IBrush MenuHover = Brush.Parse("#50000000");         // colHovered black @80
    public static readonly IBrush MenuOpen = Brush.Parse("#BA7E56");            // accent @50% saturation

    // Scrollbar (SetDarkThemeV2Colors ScrollbarBg/Grab/GrabHovered/GrabActive).
    public static readonly IBrush ScrollTrack = Brush.Parse("#87050505");       // (0.02,0.02,0.02,0.53)
    public static readonly IBrush ScrollThumb = Brush.Parse("#4F4F4F");         // 0.31
    public static readonly IBrush ScrollThumbHover = Brush.Parse("#696969");    // 0.41
    public static readonly IBrush ScrollThumbActive = Brush.Parse("#828282");   // 0.51
    public static readonly IBrush CheckMark = Brush.Parse("#C0C0C0");           // ImGuiCol_CheckMark = text

    // --- Toolbar action buttons (kept for back-compat with the .success/.danger classes) ---
    public static readonly IBrush PlayGreen = Brush.Parse("#12581E");
    public static readonly IBrush PlayGreenHover = Brush.Parse("#1A7A2E");
    public static readonly IBrush StopRed = Brush.Parse("#B91E1E");
    public static readonly IBrush StopRedHover = Brush.Parse("#D52A2A");

    // --- Colors (for places that need Color, not Brush) ---
    public static readonly Color TextColor = Color.Parse("#C0C0C0");


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

    // Hazel ships its toolbar and gizmo icons as white silhouettes and tints them at draw
    // time (UI::DrawButtonImage with Colors::Theme::text / accent). Avalonia's Image cannot
    // recolour a bitmap, so multiply the white source by the requested colour once and cache
    // the result keyed on (path, colour).
    public static Control TintedIcon(string relativePath, string fallbackPathData, Color tint, double size = 16.0)
        => TintedIcon(relativePath, fallbackPathData, new SolidColorBrush(tint), size);

    public static Control TintedIcon(string relativePath, string fallbackPathData, IBrush tint, double size = 16.0)
    {
        Color color = (tint as ISolidColorBrush)?.Color ?? Colors.White;
        Bitmap? bitmap = LoadTintedBitmap(relativePath, color);
        if (bitmap == null)
            return EditorIcons.CreatePath(fallbackPathData, tint, size);

        return new Image
        {
            Source = bitmap,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0)
        };
    }

    private static readonly Dictionary<string, Bitmap?> s_TintedBitmaps = new(StringComparer.OrdinalIgnoreCase);

    private static Bitmap? LoadTintedBitmap(string relativePath, Color tint)
    {
        string cacheKey = relativePath + "|" + tint.ToUInt32().ToString("X8");
        if (s_TintedBitmaps.TryGetValue(cacheKey, out Bitmap? cached))
            return cached;

        Bitmap? source = LoadBitmap(relativePath);
        if (source == null)
        {
            s_TintedBitmaps[cacheKey] = null;
            return null;
        }

        try
        {
            int width = source.PixelSize.Width;
            int height = source.PixelSize.Height;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            IntPtr unmanaged = Marshal.AllocHGlobal(pixels.Length);
            try
            {
                source.CopyPixels(new PixelRect(0, 0, width, height), unmanaged, pixels.Length, stride);
                Marshal.Copy(unmanaged, pixels, 0, pixels.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanaged);
            }

            for (int i = 0; i < pixels.Length; i += 4)
            {
                // BGRA premultiplied. The source is white so RGB already equals alpha where
                // the icon is opaque; scaling by the tint keeps that invariant intact.
                pixels[i + 0] = (byte)(pixels[i + 0] * tint.B / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * tint.G / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * tint.R / 255);
            }

            var tinted = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (ILockedFramebuffer frame = tinted.Lock())
            {
                long frameStart = frame.Address;
                int frameStride = frame.RowBytes;
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * stride, (IntPtr)(frameStart + (long)y * frameStride), stride);
            }

            s_TintedBitmaps[cacheKey] = tinted;
            return tinted;
        }
        catch
        {
            // Tinting is cosmetic; fall back to the untinted bitmap rather than an icon-less UI.
            s_TintedBitmaps[cacheKey] = source;
            return source;
        }
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
