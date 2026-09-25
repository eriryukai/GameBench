#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GameBench.Editor.Native;

namespace GameBench.Editor;

internal sealed class EngineViewportControl : Grid
{
    private readonly ViewportPanelViewModel _viewModel;
    private readonly Control _imageHost = new();
    private readonly TextBlock _statusText;
    private readonly Border _statusBadge;
    private readonly Action _update;
    private CompositionSurfaceVisual? _visual;
    private CompositionDrawingSurface? _surface;
    private Compositor? _compositor;
    private ICompositionGpuInterop? _gpuInterop;
    private EngineSession? _session;
    private readonly Dictionary<(ulong Generation, uint Slot), ImportedViewportFrame> _importedFrames = new();
    private PixelSize _pixelSize;
    private bool _attached;
    private bool _initialized;
    private bool _updateQueued;
    private DateTime? _busySince;

    private const string ImageType = KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle;

    public EngineViewportControl(ViewportPanelViewModel viewModel)
    {
        _viewModel = viewModel;
        _update = UpdateFrame;
        Background = Brush.Parse("#0F0F0F");
        Children.Add(_imageHost);
        _statusText = new TextBlock
        {
            Text = "Starting renderer...", Foreground = Brush.Parse("#C0C0C0"),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 620
        };
        _statusBadge = new Border
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(12), Padding = new Thickness(10, 6),
            Background = Brush.Parse("#323232"), BorderBrush = Brush.Parse("#1A1A1A"),
            BorderThickness = new Thickness(1), Child = _statusText
        };
        Children.Add(_statusBadge);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (_viewModel.Closing) return;
        if (!_viewModel.Controls.Contains(this)) _viewModel.Controls.Add(this);
        _viewModel.Active = this;
        // Children receive their visual-tree attachment after this callback returns.
        Dispatcher.UIThread.Post(InitializeComposition, DispatcherPriority.Loaded);
    }

    protected override async void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _initialized = false;
        if (_viewModel.Active == this)
            _viewModel.Active = _viewModel.Controls.LastOrDefault(c => c != this && c._attached);
        base.OnDetachedFromVisualTree(e);
        await _viewModel.Gate.WaitAsync();
        try
        {
            // A reattach may have already requested a fresh initialization.
            await ReleaseGraphicsAsync();
            if (!_attached) _viewModel.Controls.Remove(this);
        }
        finally { _viewModel.Gate.Release(); }
        if (!_viewModel.Closing)
            _viewModel.Active?.InitializeComposition();
    }

    private bool IsActive => _attached && !_viewModel.Closing && _viewModel.Active == this;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty || change.Property == IsVisibleProperty)
            QueueNextFrame();
    }

    private async void InitializeComposition()
    {
        await _viewModel.Gate.WaitAsync();
        try
        {
            if (!IsActive || _initialized) return;
            foreach (var control in _viewModel.Controls.ToArray())
                if (control != this) await control.ReleaseGraphicsAsync();
            await ReleaseGraphicsAsync();

            _compositor = ElementComposition.GetElementVisual(this)?.Compositor
                ?? throw new InvalidOperationException("Avalonia compositor is not available.");
            _surface = _compositor.CreateDrawingSurface();
            _visual = _compositor.CreateSurfaceVisual();
            _visual.Surface = _surface;
            _visual.Size = new(Bounds.Width, Bounds.Height);
            ElementComposition.SetElementChildVisual(_imageHost, _visual);
            _gpuInterop = await _compositor.TryGetCompositionGpuInterop()
                ?? throw new InvalidOperationException("GPU texture sharing is unavailable with the current graphics backend.");
            if (!_gpuInterop.SupportedImageHandleTypes.Contains(ImageType) ||
                !_gpuInterop.GetSynchronizationCapabilities(ImageType).HasFlag(
                    CompositionGpuImportedImageSynchronizationCapabilities.KeyedMutex))
                throw new InvalidOperationException("The graphics backend does not support shared keyed-mutex textures.");
            byte[] luid = _gpuInterop.DeviceLuid
                ?? throw new InvalidOperationException("The compositor did not identify its GPU.");
            _session = await Task.Run(() => EngineSession.Create(luid));
            if (!IsActive)
            {
                await ReleaseGraphicsAsync();
                return;
            }
            _initialized = true;
            SetStatus(string.Empty);
        }
        catch (Exception error)
        {
            await ReleaseGraphicsAsync();
            SetStatus(error.Message);
        }
        finally { _viewModel.Gate.Release(); }
        QueueNextFrame();
    }

    private void QueueNextFrame()
    {
        if (!IsActive || !_initialized || _updateQueued || _compositor == null) return;
        _updateQueued = true;
        _compositor.RequestCompositionUpdate(_update);
    }

    private async void UpdateFrame()
    {
        _updateQueued = false;
        await _viewModel.Gate.WaitAsync();
        try
        {
            if (!IsActive || !_initialized || _session == null || _visual == null || _surface == null) return;
            if (_gpuInterop!.IsLost) throw new InvalidOperationException("The viewport GPU was disconnected. Reset Layout to reconnect.");
            _visual.Size = new(Bounds.Width, Bounds.Height);
            var source = this.GetPresentationSource();
            if (source == null || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is Window { WindowState: WindowState.Minimized })
                return;
            var size = PixelSize.FromSize(Bounds.Size, source.RenderScaling);
            if (size.Width <= 0 || size.Height <= 0) return;
            if (size != _pixelSize)
            {
                // Gate covers the previous presentation and all asynchronous import disposal.
                await DisposeImportedFramesAsync();
                await Task.Run(() => _session.Resize((uint)size.Width, (uint)size.Height));
                _pixelSize = size;
            }
            ViewportFrame? result = await Task.Run(() => _session.Render());
            if (result is not { } frame)
            {
                _busySince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - _busySince > TimeSpan.FromSeconds(5))
                    throw new InvalidOperationException("The compositor stopped releasing viewport frames. Reset Layout to retry.");
                return;
            }
            _busySince = null;
            ImportedViewportFrame imported = GetOrImportFrame(frame);
            await _surface.UpdateWithKeyedMutexAsync(imported.Image, 1, 0);
        }
        catch (Exception error)
        {
            // Do not reuse a frame after failed import/presentation: it may never be signaled.
            await ReleaseGraphicsAsync();
            SetStatus(error.Message);
        }
        finally
        {
            _viewModel.Gate.Release();
            QueueNextFrame();
        }
    }

    private ImportedViewportFrame GetOrImportFrame(ViewportFrame frame)
    {
        if (frame.Format != 28) throw new InvalidOperationException("Unexpected native viewport texture format.");
        var key = (frame.Generation, frame.Slot);
        if (_importedFrames.TryGetValue(key, out var cached)) return cached;
        // Register before importing so partial failures are cleaned up as well.
        var imported = new ImportedViewportFrame();
        _importedFrames.Add(key, imported);
        imported.Image = _gpuInterop!.ImportImage(new PlatformHandle(frame.ImageHandle, ImageType),
            new PlatformGraphicsExternalImageProperties
            {
                Width = (int)frame.Width, Height = (int)frame.Height,
                Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm, TopLeftOrigin = true
            });
        return imported;
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
        _statusBadge.IsVisible = text.Length != 0;
    }

    // Called only with the shared gate held, after all presentation awaits have completed.
    internal async Task ReleaseGraphicsAsync()
    {
        _initialized = false;
        _updateQueued = false;
        await DisposeImportedFramesAsync();
        _surface?.Dispose();
        _surface = null;
        ElementComposition.SetElementChildVisual(_imageHost, null);
        _visual = null;
        _gpuInterop = null;
        _compositor = null;
        _pixelSize = default;
        _busySince = null;
        var session = _session;
        _session = null;
        if (session != null) await Task.Run(session.Dispose);
    }

    private async Task DisposeImportedFramesAsync()
    {
        foreach (var frame in _importedFrames.Values)
            await frame.DisposeAsync();
        _importedFrames.Clear();
    }

    private sealed class ImportedViewportFrame : IAsyncDisposable
    {
        public ICompositionImportedGpuImage Image = null!;

        public async ValueTask DisposeAsync()
        {
            // Device-loss/import failures must not prevent the remaining resources from retiring.
            foreach (IAsyncDisposable? resource in new IAsyncDisposable?[] { Image })
            {
                if (resource == null) continue;
                try { await resource.DisposeAsync(); }
                catch (Exception error) { System.Diagnostics.Trace.WriteLine(error); }
            }
        }
    }
}
