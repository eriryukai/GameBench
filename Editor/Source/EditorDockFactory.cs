#nullable enable
using System;
using System.Collections.Generic;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;

namespace GameBench.Editor;

internal sealed class EditorDockFactory : Factory
{
    private readonly MainWindowViewModel _controller;

    private IRootDock? _rootDock;

    public EditorDockFactory(MainWindowViewModel controller)
    {
        _controller = controller;
    }

    public override IRootDock CreateLayout()
    {

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "";
        rootDock.IsCollapsable = false;
        rootDock.IsFocusableRoot = true;

        _rootDock = rootDock;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
        };

        base.InitLayout(layout);
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
