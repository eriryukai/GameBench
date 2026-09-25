#nullable enable
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace GameBench.Editor;

internal sealed class EditorDockDataTemplate : IDataTemplate
{
    public Control? Build(object? data) => data switch
    {
        ViewportPanelViewModel viewport => EditorPanelViews.CreateViewport(viewport),
        _ => null
    };

    public bool Match(object? data) => data is ViewportPanelViewModel;
}

internal static class EditorPanelViews
{
    public static Control CreateViewport(ViewportPanelViewModel viewModel) =>
        new EngineViewportControl(viewModel);
}


