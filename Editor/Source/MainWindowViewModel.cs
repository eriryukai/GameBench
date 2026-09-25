#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace GameBench.Editor;

internal sealed class MainWindowViewModel
{
    public ViewportPanelViewModel Viewport { get; private set; } = new();
    public EditorDockFactory EditorFactory { get; private set; } = null!;
    public IFactory Factory { get; private set; } = null!;
    public IRootDock Layout { get; private set; } = null!;
    public string ProjectName => "GameBench";

    // Status messages land here newest-first and render in the title-bar notifications bell.
    public ObservableCollection<string> Notifications { get; } = new();

    public ViewportPlacement ViewportPlacement => EditorFactory.ViewportPlacement;

    public MainWindowViewModel() => CreateLayout();

    private void CreateLayout()
    {
        var factory = new EditorDockFactory(this);
        EditorFactory = factory;
        Factory = factory;
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);
    }

    public void ReportStatus(string status) =>
        Notifications.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {status}");

    public void ShowViewport() => EditorFactory.ShowViewport();
    public void FloatViewport() => EditorFactory.FloatViewport();
    public void HideViewport() => EditorFactory.HideViewport();

    public async Task ResetLayoutAsync()
    {
        await ShutdownAsync();
        Viewport = new ViewportPanelViewModel();
        CreateLayout();
    }

    public async Task ShutdownAsync()
    {
        await Viewport.ShutdownAsync();
        if (Layout is IDock dock && dock.Close.CanExecute(null))
            dock.Close.Execute(null);
    }
}
