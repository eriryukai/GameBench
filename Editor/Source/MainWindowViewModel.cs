#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace GameBench.Editor;

internal sealed class MainWindowViewModel
{
    public EditorDockFactory EditorFactory { get; private set; } = null!;
    public IFactory Factory { get; private set; } = null!;
    public IRootDock Layout { get; private set; } = null!;
    public string ProjectName => "GameBench";

    // Status messages land here newest-first and render in the title-bar notifications bell.
    public ObservableCollection<string> Notifications { get; } = new();

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

    public async Task ResetLayoutAsync()
    {
        await ShutdownAsync();
        CreateLayout();
    }

    public async Task ShutdownAsync()
    {
        if (Layout is IDock dock && dock.Close.CanExecute(null))
            dock.Close.Execute(null);
    }
}
