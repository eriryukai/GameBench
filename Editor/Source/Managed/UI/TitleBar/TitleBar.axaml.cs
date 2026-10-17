// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GameBench.Editor.Managed.Core;
using GameBench.Editor.Managed.Styling;

namespace GameBench.Editor.Managed.UI.TitleBar;

public partial class TitleBar : UserControl
{
    private Border _stateGradient = null!;
    private EditorState? _state;

    public TitleBar()
    {
        InitializeComponent();

        _stateGradient = this.FindControl<Border>("StateGradient")!;
        _stateGradient.Background = CreateStateGradient(EditorTheme.TitleBarStopColor);
        _stateGradient.Transitions = new Transitions
        {
            new BrushTransition
            {
                Property = Border.BackgroundProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new LinearEasing()
            }
        };

        EnableDrag(this.FindControl<StackPanel>("BrandRegion")!);
        EnableDrag(this.FindControl<Border>("DragSpacer")!);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_state != null)
            _state.PropertyChanged -= OnStatePropertyChanged;

        _state = (DataContext as TitleBarViewModel)?.State;

        if (_state != null)
        {
            _state.PropertyChanged += OnStatePropertyChanged;
            ApplyState(_state.IsPlaying);
        }
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorState.IsPlaying) && _state != null)
            ApplyState(_state.IsPlaying);
    }

    private void ApplyState(bool playing) =>
        _stateGradient.Background = CreateStateGradient(
            playing ? EditorTheme.TitleBarPlayColor : EditorTheme.TitleBarStopColor);

    private void EnableDrag(Control region)
    {
        region.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(region).Properties.IsLeftButtonPressed)
                (TopLevel.GetTopLevel(this) as Window)?.BeginMoveDrag(e);
        };
        region.DoubleTapped += (_, _) =>
        {
            if (DataContext is TitleBarViewModel viewModel)
                viewModel.Caption.ToggleMaximizeCommand.Execute(null);
        };
    }

    // The running-state overlay: a horizontal fade from the state colour back to the
    // titlebar colour, exactly like EditorLayer::UI_DrawTitlebar.
    private static IBrush CreateStateGradient(Color state) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(state, 0.0),
            new GradientStop(EditorTheme.TitleBarColor, 1.0)
        }
    };
}
