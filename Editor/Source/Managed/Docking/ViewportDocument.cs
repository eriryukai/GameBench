// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using Dock.Model.Mvvm.Controls;
using MortarEngine.Editor.Managed.UI.Viewport;

namespace MortarEngine.Editor.Managed.Docking;

// A Dock document whose content is a single persistent viewport control. Keeping
// the control on the model means docking/undocking does not recreate the engine
// session; the control only detaches when the document is actually removed.
public sealed class ViewportDocument : Document
{
    public ViewportControl View { get; } = new();
}
