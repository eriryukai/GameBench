#nullable enable
using System;
using System.Collections.Generic;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace GameBench.Editor;

internal enum ViewportPlacement
{
    Docked,
    Floating,
    Hidden
}

internal sealed class EditorDockFactory : Factory
{
    private readonly MainWindowViewModel _controller;

    private IRootDock? _rootDock;
    private IDocumentDock? _viewportDock;
    private IDockable? _viewport;

    public EditorDockFactory(MainWindowViewModel controller)
    {
        _controller = controller;
    }

    public override IRootDock CreateLayout()
    {
        var viewport = _controller.Viewport;

        var viewportDock = new DocumentDock
        {
            Id = "ViewportDock",
            Title = "Viewport",
            IsCollapsable = false,
            CanCloseLastDockable = false,
            CanCreateDocument = false,
            EnableWindowDrag = true,
            ActiveDockable = viewport,
            VisibleDockables = CreateList<IDockable>(viewport)
        };

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "";
        rootDock.IsCollapsable = false;
        rootDock.IsFocusableRoot = true;
        rootDock.VisibleDockables = CreateList<IDockable>(viewportDock);
        rootDock.DefaultDockable = viewportDock;
        rootDock.ActiveDockable = viewportDock;

        _rootDock = rootDock;
        _viewportDock = viewportDock;
        _viewport = viewport;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["ViewportDock"] = () => _viewportDock,
            ["Viewport"] = () => _viewport
        };

        base.InitLayout(layout);
    }

    // --- Viewport placement: dock / float / hide / show ------------------------------

    public ViewportPlacement ViewportPlacement
    {
        get
        {
            var viewport = _controller.Viewport;
            if (_rootDock?.HiddenDockables?.Contains(viewport) == true)
                return ViewportPlacement.Hidden;
            if (ReferenceEquals(viewport.Owner, _viewportDock))
                return ViewportPlacement.Docked;
            return viewport.Owner is IDock ? ViewportPlacement.Floating : ViewportPlacement.Hidden;
        }
    }

    public void HideViewport()
    {
        var viewport = _controller.Viewport;
        if (viewport.Closing || _rootDock is null || _viewportDock is null) return;
        if (_rootDock.HiddenDockables?.Contains(viewport) == true) return;

        if (ViewportPlacement == ViewportPlacement.Floating)
            DockViewport();
        if (!ReferenceEquals(viewport.Owner, _viewportDock)) return;

        HideDockable(viewport);
        _viewportDock.ActiveDockable = null;
        if (ReferenceEquals(_rootDock.FocusedDockable, viewport))
            SetFocusedDockable(_viewportDock, null);
    }

    public void FloatViewport()
    {
        var viewport = _controller.Viewport;
        if (viewport.Closing) return;
        if (!ReferenceEquals(viewport.Owner, _viewportDock)) return;

        FloatDockable(viewport);

        // SplitToWindow pulls the document out of the main dock without fixing its active
        // entry, which would otherwise leave the dock pointing at an invisible document.
        if (_viewportDock is not null &&
            (_viewportDock.VisibleDockables is null || !_viewportDock.VisibleDockables.Contains(viewport)))
            _viewportDock.ActiveDockable = null;
    }

    // Title bar entry point: puts the viewport back wherever it ended up.
    public void ShowViewport()
    {
        if (_controller.Viewport.Closing) return;
        switch (ViewportPlacement)
        {
            case ViewportPlacement.Floating: DockViewport(); break;
            case ViewportPlacement.Hidden: RestoreViewport(); break;
            default: ActivateViewport(); break;
        }
    }

    private void RestoreViewport()
    {
        var viewport = _controller.Viewport;
        if (_rootDock is null || _viewportDock is null) return;

        EnsureViewportDockAttached();
        if (_rootDock.HiddenDockables?.Contains(viewport) == true)
            RestoreDockable(viewport);
        if (!ReferenceEquals(viewport.Owner, _viewportDock)) return;

        _viewportDock.ActiveDockable = viewport;
        ActivateViewport();
    }

    private void DockViewport()
    {
        var viewport = _controller.Viewport;
        if (_rootDock is null || _viewportDock is null) return;
        if (ReferenceEquals(viewport.Owner, _viewportDock))
        {
            ActivateViewport();
            return;
        }

        if (viewport.Owner is not IDock sourceDock)
        {
            RestoreViewport();
            return;
        }

        EnsureViewportDockAttached();
        MoveDockable(sourceDock, _viewportDock, viewport, null);
        if (!ReferenceEquals(viewport.Owner, _viewportDock)) return;

        _viewportDock.ActiveDockable = viewport;
        ActivateViewport();
    }

    private void ActivateViewport()
    {
        if (_rootDock is null || _viewportDock is null) return;

        if (_rootDock.VisibleDockables is not null && _rootDock.VisibleDockables.Contains(_viewportDock))
            _rootDock.ActiveDockable = _viewportDock;

        _viewportDock.ActiveDockable = _controller.Viewport;
        SetActiveDockable(_controller.Viewport);
    }

    private void EnsureViewportDockAttached()
    {
        if (_rootDock is null || _viewportDock is null) return;
        if (_rootDock.VisibleDockables is null)
            _rootDock.VisibleDockables = CreateList<IDockable>();
        if (!_rootDock.VisibleDockables.Contains(_viewportDock))
            AddDockable(_rootDock, _viewportDock);
    }

    private static bool ContainsDockable(IDockable? root, IDockable target)
    {
        if (root is null) return false;
        if (ReferenceEquals(root, target)) return true;
        if (root is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
                if (ContainsDockable(child, target))
                    return true;
        }

        return false;
    }
}
