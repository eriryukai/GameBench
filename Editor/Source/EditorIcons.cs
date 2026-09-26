#nullable enable
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace GameBench.Editor;

internal static class EditorIcons
{
    public static class Toolbar
    {
        public const string Play = "M8,5.14V19.14L19,12.14L8,5.14Z";
        public const string Stop = "M18,18H6V6H18V18Z";
    }

    public static class Common
    {
        public const string Close = "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z";
    }

    public static class Notifications
    {
        public const string Bell = "M21 19v1H3v-1l2-2v-6c0-3.07 1.64-5.64 4.5-6.32V4c0-.83.67-1.5 1.5-1.5s1.5.67 1.5 1.5v.68C15.36 5.36 17 7.92 17 11v6l2 2zm-7 2c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2z";
    }

    private const double DefaultIconSize = 18;

    public static Path CreatePath(string pathData, IBrush? brush = null, double size = DefaultIconSize)
    {
        return new Path
        {
            Data = StreamGeometry.Parse(pathData),
            Fill = brush ?? Brushes.White,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(4, 0)
        };
    }
}
