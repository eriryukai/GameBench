// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.Core;
using MortarEngine.Editor.Managed.Core;
using MortarEngine.Editor.Managed.Docking;
using MortarEngine.Editor.Managed.UI.Caption;
using MortarEngine.Editor.Managed.UI.TitleBar;
using MortarEngine.Editor.Managed.UI.Toolbar;
using MortarEngine.Editor.Managed.ViewModels;

namespace MortarEngine.Editor.Managed.UI;

// Root view model for the editor shell. It composes the region view models and owns the
// dock layout, so MainWindow itself stays a thin composition surface.
public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(WindowCaptionViewModel caption)
    {
        TitleBar = new TitleBarViewModel(State, caption);
        Transport = new TransportViewModel(State);
        CreateLayout();
    }

    public EditorState State { get; } = new();

    public string ProjectName => "MortarEngine";

    public string Title => $"MortarEngine Editor - {ProjectName}";

    public TitleBarViewModel TitleBar { get; }

    public TransportViewModel Transport { get; }

    public EditorDockFactory EditorFactory { get; private set; } = null!;

    public IFactory Factory { get; private set; } = null!;

    public IRootDock Layout { get; private set; } = null!;

    public void ReportStatus(string status) => State.ReportStatus(status);

    public async Task ResetLayoutAsync()
    {
        await ShutdownAsync();
        CreateLayout();
    }

    public Task ShutdownAsync()
    {
        if (Layout is IDock dock && dock.Close.CanExecute(null))
            dock.Close.Execute(null);
        return Task.CompletedTask;
    }

    private void CreateLayout()
    {
        var factory = new EditorDockFactory();
        EditorFactory = factory;
        Factory = factory;
        Layout = factory.CreateLayout();
        factory.InitLayout(Layout);
    }
}
