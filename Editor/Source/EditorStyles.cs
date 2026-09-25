#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Dock.Avalonia.Controls;

namespace GameBench.Editor;

// Code-built global styles that retheme the Fluent base into a flat Hazel/Unreal dark
// look. Button accents are class-driven (.accent/.success/.danger/.toolbar) so their
// hover/pressed states come from styles instead of inline brushes. Item/menu state
// colors are set per pseudo-class. Avalonia stores a Style frame ahead of a ControlTheme
// frame for the same binding priority (FramePriority.Style = 9, StyleTheme = 11) and
// evaluates ValueStore from the high-precedence end, so these beat Fluent's defaults -
// including the ones that target /template/ parts.
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
        // Default button: ImGuiCol_Button ImColor(56,56,56,200) - flat, borderless.
        Add(Make(x => x.OfType<Button>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.ButtonFace),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)),
            (TemplatedControl.PaddingProperty, new Thickness(10, 5)),
            (TemplatedControl.FontSizeProperty, 12.0)));
        Add(Make(x => x.OfType<Button>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.Hover)));
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
        // Flat square box: #0F0F0F field with a #1A1A1A outline, accent fill and a light
        // tick when checked (ImGuiCol_CheckMark = text).
        Add(Make(x => x.OfType<CheckBox>().Template().OfType<Border>().Name("NormalRectangle"),
            (Border.BackgroundProperty, EditorTheme.PropertyField),
            (Border.BorderBrushProperty, EditorTheme.Border)));
        Add(Make(x => x.OfType<CheckBox>().Class(":pointerover").Template().OfType<Border>().Name("NormalRectangle"),
            (Border.BackgroundProperty, EditorTheme.PropertyField),
            (Border.BorderBrushProperty, EditorTheme.BorderStrong)));
        Add(Make(x => x.OfType<CheckBox>().Class(":checked").Template().OfType<Border>().Name("NormalRectangle"),
            (Border.BackgroundProperty, EditorTheme.Accent),
            (Border.BorderBrushProperty, EditorTheme.Accent)));
        Add(Make(x => x.OfType<CheckBox>().Template().OfType<Path>().Name("CheckGlyph"),
            (Shape.FillProperty, EditorTheme.CheckMark)));

        Add(Make(x => x.OfType<ComboBox>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.PropertyField),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Border),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(3))));
        Add(Make(x => x.OfType<ComboBox>().Class(":pointerover"),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong)));
        Add(Make(x => x.OfType<ComboBox>().Class(":focus"),
            (TemplatedControl.BorderBrushProperty, EditorTheme.Accent)));
    }

    private void AddItems()
    {
        // Selected rows are filled solid gold with dark text, exactly like
        // ContentBrowserPanel; hover darkens the gold instead of tinting it blue.
        Add(Make(x => x.OfType<TreeViewItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.PaddingProperty, new Thickness(2, 1))));
        Add(Make(x => x.OfType<TreeViewItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.GroupHeader)));
        Add(Make(x => x.OfType<TreeViewItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRow),
            (TemplatedControl.ForegroundProperty, EditorTheme.BackgroundDark)));
        Add(Make(x => x.OfType<TreeViewItem>().Class(":selected").Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRowHover),
            (TemplatedControl.ForegroundProperty, EditorTheme.BackgroundDark)));

        // Content browser grid rows (ItemsControl items).
        Add(Make(x => x.OfType<ListBoxItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(3)),
            (TemplatedControl.PaddingProperty, new Thickness(2, 1))));
        Add(Make(x => x.OfType<ListBoxItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.GroupHeader)));
        Add(Make(x => x.OfType<ListBoxItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRow),
            (TemplatedControl.ForegroundProperty, EditorTheme.BackgroundDark)));
        Add(Make(x => x.OfType<ListBoxItem>().Class(":selected").Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.SelectionRowHover),
            (TemplatedControl.ForegroundProperty, EditorTheme.BackgroundDark)));

        // GridSplitter for content browser panels.
        Add(Make(x => x.OfType<GridSplitter>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Splitter)));
    }

    private void AddMenusAndPopups()
    {
        // The menubar sits directly on the titlebar gradient, so it paints nothing itself.
        Add(Make(x => x.OfType<Menu>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent)));

        Add(Make(x => x.OfType<MenuItem>(),
            (TemplatedControl.ForegroundProperty, EditorTheme.Text),
            (TemplatedControl.PaddingProperty, new Thickness(8, 4))));
        Add(Make(x => x.OfType<MenuItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.MenuHover)));
        Add(Make(x => x.OfType<MenuItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.MenuHover)));

        // UI_DrawMenubar: an entry with its dropdown open fills with the accent at 50%
        // saturation and flips its label to backgroundDark. Declared last so it wins
        // over the hover/selection brushes on the same element.
        Add(Make(x => x.OfType<MenuItem>().Class(":open"),
            (TemplatedControl.BackgroundProperty, EditorTheme.MenuOpen),
            (TemplatedControl.ForegroundProperty, EditorTheme.BackgroundDark)));

        // Flyout / context-menu surfaces.
        Add(Make(x => x.OfType<MenuFlyoutPresenter>(),
            (TemplatedControl.BackgroundProperty, EditorTheme.Surface),
            (TemplatedControl.BorderBrushProperty, EditorTheme.BorderStrong),
            (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            (TemplatedControl.CornerRadiusProperty, new CornerRadius(5))));
        Add(Make(x => x.OfType<FlyoutPresenter>(),
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
        Add(Make(x => x.OfType<DocumentDockControl>(),
            (Visual.IsVisibleProperty, new Binding("IsEmpty") { Converter = BoolConverters.Not })));

        // Hazel tab fills: ImGuiCol_TabHovered = ImColor(255,225,135,30),
        // ImGuiCol_TabActive = ImColor(255,225,135,60).
        Add(Make(x => x.OfType<TabItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextMuted),
            (TemplatedControl.FontSizeProperty, 13.0)));
        Add(Make(x => x.OfType<TabItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabHover)));
        Add(Make(x => x.OfType<TabItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabActive),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextBright)));

        // Dock.Avalonia's own strip items use the same three-state fill so document and
        // tool tabs read as one system.
        Add(Make(x => x.OfType<DocumentTabStripItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextMuted)));
        Add(Make(x => x.OfType<DocumentTabStripItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabHover)));
        Add(Make(x => x.OfType<DocumentTabStripItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabActive),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextBright)));

        Add(Make(x => x.OfType<ToolTabStripItem>(),
            (TemplatedControl.BackgroundProperty, Brushes.Transparent),
            (TemplatedControl.ForegroundProperty, EditorTheme.TextMuted)));
        Add(Make(x => x.OfType<ToolTabStripItem>().Class(":pointerover"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabHover)));
        Add(Make(x => x.OfType<ToolTabStripItem>().Class(":selected"),
            (TemplatedControl.BackgroundProperty, EditorTheme.TabActive),
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
