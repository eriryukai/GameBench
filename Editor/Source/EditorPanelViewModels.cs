#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dock.Model.Mvvm.Controls;

namespace GameBench.Editor;

internal sealed class ViewportPanelViewModel : Document
{
    // All controls produced by docking share one owner and serialize their GPU lifetime.
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal readonly List<EngineViewportControl> Controls = new();
    internal EngineViewportControl? Active;
    internal bool Closing;

    public ViewportPanelViewModel()
    {
        Id = "Viewport";
        Title = "Viewport";
        CanClose = false;
    }

    public async Task ShutdownAsync()
    {
        Closing = true;
        Active = null;
        await Gate.WaitAsync();
        try
        {
            foreach (var control in Controls.ToArray())
                await control.ReleaseGraphicsAsync();
            Controls.Clear();
        }
        finally { Gate.Release(); }
    }
}

