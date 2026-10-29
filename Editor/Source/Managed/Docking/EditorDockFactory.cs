// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Collections.Generic;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;

namespace MortarEngine.Editor.Managed.Docking;

public sealed class EditorDockFactory : Factory
{
    private IRootDock? _rootDock;

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
}
