#nullable enable
using System;
using System.Collections.Generic;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace GameBench.Editor;

internal sealed class EditorDockFactory : Factory
{
    private readonly MainWindowViewModel _controller;

    private IRootDock? _rootDock;
    private IDocumentDock? _viewportDock;
    private IToolDock? _sceneHierarchyDock;
    private IToolDock? _detailsDock;
    private IToolDock? _contentBrowserDock;
    private IDockable? _viewport;
    private IDockable? _sceneHierarchy;
    private IDockable? _details;
    private IDockable? _contentBrowser;
    private IDockable? _profiler;

    public EditorDockFactory(MainWindowViewModel controller)
    {
        _controller = controller;
    }

    public override IRootDock CreateLayout()
    {
        var viewport = _controller.Viewport;

        var sceneHierarchy = new SceneHierarchyPanelViewModel()
        {
            Id = "SceneHierarchy",
            Title = "Scene Hierarchy",
            CanClose = false
        };

        var details = new DetailsPanelViewModel()
        {
            Id = "Details",
            Title = "Details",
            CanClose = false
        };

        var contentBrowser = new ContentBrowserPanelViewModel()
        {
            Id = "ContentBrowser",
            Title = "Content Browser",
            CanClose = false
        };

        var profiler = new ProfilerPanelViewModel()
        {
            Id = "Profiler",
            Title = "Profiler",
            CanClose = false
        };

        var viewportDock = new DocumentDock
        {
            Id = "ViewportDock",
            Title = "Viewport",
            IsCollapsable = false,
            CanCloseLastDockable = false,
            CanCreateDocument = false,
            EnableWindowDrag = true,
            Proportion = 0.72,
            ActiveDockable = viewport,
            VisibleDockables = CreateList<IDockable>(viewport)
        };

        var contentBrowserDock = new ToolDock
        {
            Id = "ContentBrowserDock",
            Title = "Content Browser",
            Alignment = Alignment.Bottom,
            GripMode = GripMode.Visible,
            Proportion = 0.28,
            ActiveDockable = contentBrowser,
            VisibleDockables = CreateList<IDockable>(contentBrowser, profiler)
        };

        var centerDock = new ProportionalDock
        {
            Id = "CenterDock",
            Title = "Center",
            Orientation = Orientation.Vertical,
            Proportion = 0.62,
            ActiveDockable = viewportDock,
            VisibleDockables = CreateList<IDockable>
            (
                viewportDock,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                contentBrowserDock
            )
        };

        var sceneHierarchyDock = new ToolDock
        {
            Id = "SceneHierarchyDock",
            Title = "Scene Hierarchy",
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.18,
            ActiveDockable = sceneHierarchy,
            VisibleDockables = CreateList<IDockable>(sceneHierarchy)
        };

        var detailsDock = new ToolDock
        {
            Id = "DetailsDock",
            Title = "Details",
            Alignment = Alignment.Right,
            GripMode = GripMode.Visible,
            Proportion = 0.20,
            ActiveDockable = details,
            VisibleDockables = CreateList<IDockable>(details)
        };

        var mainLayout = new ProportionalDock
        {
            Id = "EditorMainLayout",
            Title = "Editor Layout",
            IsCollapsable = false,
            Orientation = Orientation.Horizontal,
            ActiveDockable = centerDock,
            VisibleDockables = CreateList<IDockable>
            (
                sceneHierarchyDock,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                centerDock,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                detailsDock
            )
        };

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "GameBench Editor";
        rootDock.IsCollapsable = false;
        rootDock.IsFocusableRoot = true;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.DefaultDockable = mainLayout;
        rootDock.ActiveDockable = mainLayout;

        _rootDock = rootDock;
        _viewportDock = viewportDock;
        _sceneHierarchyDock = sceneHierarchyDock;
        _detailsDock = detailsDock;
        _contentBrowserDock = contentBrowserDock;
        _viewport = viewport;
        _sceneHierarchy = sceneHierarchy;
        _details = details;
        _contentBrowser = contentBrowser;
        _profiler = profiler;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["ViewportDock"] = () => _viewportDock,
            ["SceneHierarchyDock"] = () => _sceneHierarchyDock,
            ["DetailsDock"] = () => _detailsDock,
            ["ContentBrowserDock"] = () => _contentBrowserDock,
            ["Viewport"] = () => _viewport,
            ["SceneHierarchy"] = () => _sceneHierarchy,
            ["Details"] = () => _details,
            ["ContentBrowser"] = () => _contentBrowser,
            ["Profiler"] = () => _profiler
        };

        base.InitLayout(layout);
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);
        if (window is not null)
            window.Title = "GameBench Editor";

        return window;
    }
}
