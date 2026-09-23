#nullable enable
using System;
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace GameBench.Editor;

internal sealed class MainWindowViewModel
{
    public ViewportPanelViewModel Viewport { get; private set; } = new();
    public IFactory Factory { get; private set; } = null!;
    public IRootDock Layout { get; private set; } = null!;
    public string ProjectName => "GameBench";
    public event Action<string>? StatusChanged;

    public MainWindowViewModel() => CreateLayout();

    private void CreateLayout()
    {
        var factory = new EditorDockFactory(this);
        Factory = factory;
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);
    }

    public void ReportStatus(string status) => StatusChanged?.Invoke(status);

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
