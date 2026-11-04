// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MortarEngine.Editor.Native.EditorIpc;

namespace MortarEngine.Editor.Managed.UI.Viewport;

// Avalonia side of the viewport. It owns a CompositionDrawingSurface and only
// imports the shared textures produced by the native renderer; it never records
// D3D12 commands or renders engine frames itself.
public sealed class ViewportControl : Control
{
    private Compositor? _compositor;
    private ICompositionGpuInterop? _interop;
    private CompositionDrawingSurface? _surface;
    private CompositionSurfaceVisual? _visual;
    private EditorSession? _session;
    private readonly ICompositionImportedGpuImage?[] _images = new ICompositionImportedGpuImage?[3];

    private CancellationTokenSource? _initCts;
    private readonly SemaphoreSlim _presentLock = new(1, 1);
    private bool _initializing;
    private bool _disposed;
    private string? _status = "Connecting to engine...";

    public ViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_initializing || _session != null)
        {
            return;
        }

        // Dock may keep the document control alive while its tab is hidden
        // (CacheDocumentTabContent). Re-attaching starts a fresh engine session.
        _disposed = false;
        _status = "Connecting to engine...";
        _initializing = true;
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();
        _ = InitializeAsync(_initCts.Token);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _disposed = true;
        _initCts?.Cancel();

        var session = Interlocked.Exchange(ref _session, null);
        if (session != null)
        {
            session.FrameReady -= OnFrameReady;
            session.Disconnected -= OnDisconnected;
            _ = DisposeSessionAsync(session);
        }

        _ = DisposeImagesAsync();

        if (_surface != null)
        {
            _surface.Dispose();
            _surface = null;
        }

        if (_visual != null)
        {
            ElementComposition.SetElementChildVisual(this, null);
            _visual = null;
        }

        _interop = null;
        _compositor = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty && _visual != null)
        {
            _visual.Size = new(Bounds.Width, Bounds.Height);
        }
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Black, null, new Rect(Bounds.Size));

        if (_status != null)
        {
            var text = new FormattedText(
                _status,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                13,
                Brushes.Gainsboro);

            context.DrawText(text, new Point(16, 16));
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var visual = ElementComposition.GetElementVisual(this);
            if (visual == null)
            {
                SetStatus("Viewport is not attached to a composition visual.");
                return;
            }

            _compositor = visual.Compositor;
            _interop = await _compositor.TryGetCompositionGpuInterop();
            if (_interop == null)
            {
                SetStatus("The current Avalonia rendering backend does not expose GPU interop.");
                return;
            }

            if (!_interop.SupportedImageHandleTypes.Contains(
                    KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle))
            {
                SetStatus("This backend does not support D3D11TextureNtHandle imports.");
                return;
            }

            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            var pixelSize = PixelSize.FromSize(Bounds.Size, scaling);
            // Bounds are zero during the first attach pass. Start with a sane
            // render size; the composition visual is resized when layout runs.
            var width = pixelSize.Width > 1 ? pixelSize.Width : 1280;
            var height = pixelSize.Height > 1 ? pixelSize.Height : 720;

            // The engine renders at the viewport's initial pixel size. Resizing
            // the shared ring is deliberately deferred; the composition visual
            // scales the current frame while the dock is resized.
            var session = await EditorSession.StartAsync(
                _interop.DeviceLuid,
                width,
                height,
                cancellationToken);

            if (_disposed)
            {
                await session.DisposeAsync();
                return;
            }

            _session = session;
            session.FrameReady += OnFrameReady;
            session.Disconnected += OnDisconnected;

            var hello = session.Hello;
            for (var index = 0; index < 3; ++index)
            {
                var localHandle = session.DuplicateSharedHandle(hello.GetSharedHandle(index));
                try
                {
                    var image = _interop.ImportImage(
                        new PlatformHandle(
                            localHandle,
                            KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle),
                        new PlatformGraphicsExternalImageProperties
                        {
                            Width = (int)hello.Width,
                            Height = (int)hello.Height,
                            Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                            TopLeftOrigin = true,
                        });

                    await image.ImportCompleted;
                    _images[index] = image;
                }
                finally
                {
                    NativeMethods.CloseHandle(localHandle);
                }
            }

            if (_disposed)
            {
                await DisposeImagesAsync();
                return;
            }

            _surface = _compositor.CreateDrawingSurface();
            _visual = _compositor.CreateSurfaceVisual();
            _visual.Size = new(Bounds.Width, Bounds.Height);
            _visual.Surface = _surface;
            ElementComposition.SetElementChildVisual(this, _visual);

            // Only start reading once every slot has an imported image and the
            // composition surface exists. Otherwise an early frame would have
            // to be acked without acquiring/releasing its keyed-mutex key.
            session.StartReadLoop();

            SetStatus(null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            SetStatus(error.Message);
        }
        finally
        {
            _initializing = false;
        }
    }

    private void OnFrameReady(uint slot, ulong frameId)
    {
        Dispatcher.UIThread.Post(() => QueueFrame(slot, frameId));
    }

    private void OnDisconnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_visual != null)
            {
                ElementComposition.SetElementChildVisual(this, null);
                _visual = null;
            }

            SetStatus("Engine disconnected.");
        });
    }

    private void QueueFrame(uint slot, ulong frameId)
    {
        if (_disposed)
        {
            return;
        }

        _ = PresentFrameAsync(slot, frameId);
    }

    private async Task PresentFrameAsync(uint slot, ulong frameId)
    {
        // Serialize imports because every frame must acquire/release the
        // keyed mutex exactly once before its slot can be acknowledged.
        await _presentLock.WaitAsync();
        try
        {
            if (_disposed || _surface == null)
            {
                return;
            }

            var image = slot < _images.Length ? _images[slot] : null;
            if (image == null)
            {
                return;
            }

            // The engine acquires key 0 before rendering and releases key 1.
            // Avalonia acquires 1, copies the image, and releases 0.
            await _surface.UpdateWithKeyedMutexAsync(image, 1, 0);
            await AckSafelyAsync(slot, frameId);
        }
        catch (Exception error)
        {
            SetStatus(error.Message);
        }
        finally
        {
            _presentLock.Release();
        }
    }

    private async Task AckSafelyAsync(uint slot, ulong frameId)
    {
        var session = _session;
        if (session == null)
        {
            return;
        }

        try
        {
            await session.SendFrameAckAsync(slot, frameId);
        }
        catch
        {
            // A broken pipe is surfaced by the Disconnected event.
        }
    }

    private async Task DisposeSessionAsync(EditorSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch
        {
            // Best effort during teardown.
        }
    }

    private async Task DisposeImagesAsync()
    {
        for (var index = 0; index < _images.Length; ++index)
        {
            var image = _images[index];
            _images[index] = null;
            if (image == null)
            {
                continue;
            }

            try
            {
                await image.DisposeAsync();
            }
            catch
            {
                // Best effort during teardown.
            }
        }
    }

    private void SetStatus(string? status)
    {
        _status = status;
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

}
