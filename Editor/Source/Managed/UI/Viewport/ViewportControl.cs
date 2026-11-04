// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private int _renderWidth = 1280;
    private int _renderHeight = 720;
    private string? _status = "Connecting to engine...";

    public ViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;

        // Use tunnel handlers with handledEventsToo so a Dock/document handler
        // cannot swallow the press before the engine viewport sees it.
        AddHandler(PointerPressedEvent, OnViewportPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnViewportPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(PointerWheelChangedEvent, OnViewportPointerWheelChanged, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, OnViewportKeyDown, RoutingStrategies.Tunnel, true);
        AddHandler(KeyUpEvent, OnViewportKeyUp, RoutingStrategies.Tunnel, true);
        AddHandler(TextInputEvent, OnViewportTextInput, RoutingStrategies.Tunnel, true);
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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SendMouseMove(e);
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        e.Pointer.Capture(this);
        SendMouseMove(e);

        var button = ToMouseButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind);
        if (button < 0)
        {
            return;
        }

        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.MouseButton,
            Button = button,
            Down = 1,
            Modifiers = ToModifiers(e.KeyModifiers),
        });
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SendMouseMove(e);

        var button = ToMouseButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind);
        if (button >= 0)
        {
            SendInput(new InputPayload
            {
                Kind = (uint)ViewportInputType.MouseButton,
                Button = button,
                Down = 0,
                Modifiers = ToModifiers(e.KeyModifiers),
            });
        }

        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.MouseWheel,
            Wheel = (float)e.Delta.Y,
            Modifiers = ToModifiers(e.KeyModifiers),
        });
        e.Handled = true;
    }

    private void OnViewportKeyDown(object? sender, KeyEventArgs e)
    {
        var key = ToViewportKey(e.Key);
        if (key == ViewportKey.None)
        {
            return;
        }

        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.Key,
            Key = (int)key,
            Down = 1,
            Modifiers = ToModifiers(e.KeyModifiers),
        });
        e.Handled = true;
    }

    private void OnViewportKeyUp(object? sender, KeyEventArgs e)
    {
        var key = ToViewportKey(e.Key);
        if (key == ViewportKey.None)
        {
            return;
        }

        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.Key,
            Key = (int)key,
            Down = 0,
            Modifiers = ToModifiers(e.KeyModifiers),
        });
        e.Handled = true;
    }

    private void OnViewportTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        for (var index = 0; index < e.Text.Length;)
        {
            var codepoint = char.ConvertToUtf32(e.Text, index);
            SendInput(new InputPayload
            {
                Kind = (uint)ViewportInputType.Text,
                Codepoint = (uint)codepoint,
            });
            index += char.IsSurrogatePair(e.Text, index) ? 2 : 1;
        }

        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.Focus,
            Down = 1,
        });
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.Focus,
            Down = 0,
        });
    }

    private void SendMouseMove(PointerEventArgs e)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var position = e.GetPosition(this);
        SendInput(new InputPayload
        {
            Kind = (uint)ViewportInputType.MouseMove,
            X = (float)(position.X / Bounds.Width * _renderWidth),
            Y = (float)(position.Y / Bounds.Height * _renderHeight),
            Modifiers = ToModifiers(e.KeyModifiers),
        });
    }

    private void SendInput(InputPayload payload)
    {
        var session = _session;
        if (session == null || _disposed)
        {
            return;
        }

        _ = SendInputCoreAsync(session, payload);
    }

    private static async Task SendInputCoreAsync(EditorSession session, InputPayload payload)
    {
        try
        {
            await session.SendInputAsync(payload);
        }
        catch
        {
            // A disconnected session is surfaced through the frame path.
        }
    }

    private static uint ToModifiers(KeyModifiers modifiers) => (uint)modifiers;

    private static int ToMouseButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => 0,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => 1,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => 2,
        PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => 3,
        PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => 4,
        _ => -1,
    };

    private static ViewportKey ToViewportKey(Avalonia.Input.Key key)
    {
        if (key >= Avalonia.Input.Key.A && key <= Avalonia.Input.Key.Z)
        {
            return (ViewportKey)((int)ViewportKey.A + ((int)key - (int)Avalonia.Input.Key.A));
        }

        if (key >= Avalonia.Input.Key.D0 && key <= Avalonia.Input.Key.D9)
        {
            return (ViewportKey)((int)ViewportKey.D0 + ((int)key - (int)Avalonia.Input.Key.D0));
        }

        if (key >= Avalonia.Input.Key.F1 && key <= Avalonia.Input.Key.F12)
        {
            return (ViewportKey)((int)ViewportKey.F1 + ((int)key - (int)Avalonia.Input.Key.F1));
        }

        return key switch
        {
            Avalonia.Input.Key.Enter => ViewportKey.Enter,
            Avalonia.Input.Key.Escape => ViewportKey.Escape,
            Avalonia.Input.Key.Tab => ViewportKey.Tab,
            Avalonia.Input.Key.Back => ViewportKey.Backspace,
            Avalonia.Input.Key.Space => ViewportKey.Space,
            Avalonia.Input.Key.Left => ViewportKey.Left,
            Avalonia.Input.Key.Right => ViewportKey.Right,
            Avalonia.Input.Key.Up => ViewportKey.Up,
            Avalonia.Input.Key.Down => ViewportKey.Down,
            Avalonia.Input.Key.Home => ViewportKey.Home,
            Avalonia.Input.Key.End => ViewportKey.End,
            Avalonia.Input.Key.PageUp => ViewportKey.PageUp,
            Avalonia.Input.Key.PageDown => ViewportKey.PageDown,
            Avalonia.Input.Key.Delete => ViewportKey.Delete,
            Avalonia.Input.Key.Insert => ViewportKey.Insert,
            Avalonia.Input.Key.LeftShift => ViewportKey.LeftShift,
            Avalonia.Input.Key.RightShift => ViewportKey.RightShift,
            Avalonia.Input.Key.LeftCtrl => ViewportKey.LeftCtrl,
            Avalonia.Input.Key.RightCtrl => ViewportKey.RightCtrl,
            Avalonia.Input.Key.LeftAlt => ViewportKey.LeftAlt,
            Avalonia.Input.Key.RightAlt => ViewportKey.RightAlt,
            Avalonia.Input.Key.OemComma => ViewportKey.Comma,
            Avalonia.Input.Key.OemPeriod => ViewportKey.Period,
            Avalonia.Input.Key.OemMinus => ViewportKey.Minus,
            Avalonia.Input.Key.OemPlus => ViewportKey.Equal,
            Avalonia.Input.Key.OemSemicolon => ViewportKey.Semicolon,
            Avalonia.Input.Key.OemQuestion => ViewportKey.Slash,
            Avalonia.Input.Key.OemPipe => ViewportKey.Backslash,
            Avalonia.Input.Key.OemQuotes => ViewportKey.Apostrophe,
            Avalonia.Input.Key.OemTilde => ViewportKey.Grave,
            Avalonia.Input.Key.OemOpenBrackets => ViewportKey.LeftBracket,
            Avalonia.Input.Key.OemCloseBrackets => ViewportKey.RightBracket,
            _ => ViewportKey.None,
        };
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
            _renderWidth = Math.Max(1, (int)hello.Width);
            _renderHeight = Math.Max(1, (int)hello.Height);

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
