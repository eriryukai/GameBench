#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace GameBench.Editor;

// Code-built global styles that retheme the Fluent base into a flat Hazel/Unreal dark
// look. Button accents are class-driven (.accent/.success/.danger/.toolbar) so their
// hover/pressed states come from styles instead of inline brushes. Item/menu state
// colors are set per pseudo-class; Avalonia resolves Styles above the base ControlTheme,
// so these win over Fluent's defaults.
internal sealed class EditorStyles : Styles
{
    public EditorStyles()
    {
        // Base text + panels
        Add(Make(x => x.OfType<TextBlock>(),
            (TextBlock.ForegroundProperty, EditorTheme.Text)));

        AddButtons();
        AddInputs();
        AddItems();
        AddMenusAndPopups();
        AddScrollAndTabs();
        AddTooltip();
    }

    private void AddButtons()
    {
        // Default button: flat graphite surface.
        Add(Make(x => x.OfType<Button>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Surface),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Border),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
            (TemplatedControl.PaddingProperty, new Thickness(10, 5)),
            (TemplatedControl.FontSizeProperty, 12.0)));
        Add(Make(x => x.OfType<Button>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong)));
        Add(Make(x => x.OfType<Button>().Class(":pressed"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Pressed)));

        AddAccentButton("accent", EditorTheme.Accent, EditorTheme.AccentHover, EditorTheme.AccentPressed);
        AddAccentButton("success", EditorTheme.PlayGreen, EditorTheme.PlayGreenHover, EditorTheme.PlayGreen);
        AddAccentButton("danger", EditorTheme.StopRed, EditorTheme.StopRedHover, EditorTheme.StopRed);

        // Flat toolbar button (icon buttons in toolbars/overlays).
        Add(Make(x => x.OfType<Button>().Class("toolbar"),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
            (TemplatedControl.PaddingProperty, new Thickness(7, 5))));
        Add(Make(x => x.OfType<Button>().Class("toolbar").Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));
        Add(Make(x => x.OfType<Button>().Class("toolbar").Class(":pressed"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Pressed)));

        // Toggled tool (e.g. selected transform tool) — gold-tinted active state.
        Add(Make(x => x.OfType<Button>().Class("toolbar").Class("active"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRow),
            (TemplatedControl.ForegroundProperty, EditorTheme.Selection)));

        // Custom window caption buttons (min / max / close in our undecorated title bar).
        Add(Make(x => x.OfType<Button>().Class("caption"),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
            (TemplatedControl.PaddingProperty, new Thickness(0))));
        Add(Make(x => x.OfType<Button>().Class("caption").Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));
        Add(Make(x => x.OfType<Button>().Class("caption").Class(":pressed"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Pressed)));

        // Close button turns red on hover, like a standard window control.
        Add(Make(x => x.OfType<Button>().Class("caption-close").Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.StopRed)));
        Add(Make(x => x.OfType<Button>().Class("caption-close").Class(":pressed"),
            (TemplatedControl.BackgroundProperty, EditorTheme.StopRedHover)));
    }

    private void AddAccentButton(string cls, IBrush bg, IBrush hover, IBrush pressed)
    {
        Add(Make(x => x.OfType<Button>().Class(cls),
            (TemplatedControl.BackgroundProperty, bg),
            (TemplatedControl.ForegroundProperty, Brushes.White),
            (TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
            (TemplatedControl.PaddingProperty, new Thickness(12, 5))));
        Add(Make(x => x.OfType<Button>().Class(cls).Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, hover)));
        Add(Make(x => x.OfType<Button>().Class(cls).Class(":pressed"),
            (TemplatedControl.BackgroundProperty, pressed)));
    }

    private void AddInputs()
    {
        Add(Make(x => x.OfType<TextBox>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.PropertyField),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextBright),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Border),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(3)),
            (TemplatedControl.FontSizeProperty, 12.0),
            (TextBox.CaretBrushProperty, EditorTheme.TextBright)));
        Add(Make(x => x.OfType<TextBox>().Class(":pointerover"),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong)));
        Add(Make(x => x.OfType<TextBox>().Class(":focus"),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Accent)));

        Add(Make(x => x.OfType<CheckBox>(),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text)));

        Add(Make(x => x.OfType<ComboBox>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.PropertyField),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Border),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(3))));
    }

    private void AddItems()
    {
        // TreeView rows keep their custom header chrome; clear the container so Fluent's
        // accent selection doesn't double up under the gold header highlight.
        Add(Make(x => x.OfType<TreeViewItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.PaddingProperty, new Thickness(2, 1))));
        Add(Make(x => x.OfType<TreeViewItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent)));
        Add(Make(x => x.OfType<TreeViewItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent)));

        // Content browser grid rows (ItemsControl items).
        Add(Make(x => x.OfType<ListBoxItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(3)),
            (TemplatedControl.PaddingProperty, new Thickness(2, 1))));
        Add(Make(x => x.OfType<ListBoxItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));
        Add(Make(x => x.OfType<ListBoxItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRow),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextBright)));

        // GridSplitter for content browser panels.
        Add(Make(x => x.OfType<GridSplitter>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Border)));
    }

    private void AddMenusAndPopups()
    {
        Add(Make(x => x.OfType<Menu>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.BackgroundDark)));

        Add(Make(x => x.OfType<MenuItem>(),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.PaddingProperty, new Thickness(8, 4))));
        Add(Make(x => x.OfType<MenuItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));
        Add(Make(x => x.OfType<MenuItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));

        // Flyout / context-menu surfaces.
        Add(Make(x => x.OfType<MenuFlyoutPresenter>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Surface),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(5))));
        Add(Make(x => x.OfType<ContextMenu>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Surface),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(5))));
    }

    private void AddScrollAndTabs()
    {
        Add(Make(x => x.OfType<TabItem>(),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextMuted),
            (TemplatedControl.FontSizeProperty, 13.0)));
        Add(Make(x => x.OfType<TabItem>().Class(":selected"),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextBright)));
    }

    private void AddTooltip()
    {
        Add(Make(x => x.OfType<ToolTip>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Surface),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
            (TemplatedControl.FontSizeProperty, 12.0)));
    }

    private static Style Make(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object? Value)[] setters)
    {
        var style = new Style(selector);
        foreach (var (property, value) in setters)
            style.Setters.Add(new Setter(property, value));
        return style;
    }
}
