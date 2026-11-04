# Native interop

Everything that talks to the native C++ engine (`Engine.exe`) from the managed
editor lives here. The managed editor never renders engine frames itself.

Current pieces:

- `EditorIpc/EditorProtocol.cs` mirrors `Engine/Source/Private/Interop/EditorProtocol.h`.
- `EditorIpc/EditorSession.cs` owns the named-pipe server, launches `Engine.exe`
  in editor mode, reads frame-ready messages, and converts engine NT handles into
  handles valid in the editor process with `DuplicateHandle`.
- `EditorIpc/NativeMethods.cs` contains the Win32 handle P/Invokes.
- `../Managed/UI/Viewport/ViewportControl.cs` owns the Avalonia
  `CompositionDrawingSurface` and imports the shared D3D12 textures as
  `D3D11TextureNtHandle`.

## Frame protocol

Avalonia's Windows/ANGLE importer requires `IDXGIKeyedMutex`, which D3D12 cannot
create directly. The ring therefore creates three D3D11 textures with
`D3D11_RESOURCE_MISC_SHARED_NTHANDLE | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX`;
D3D12 opens the same allocations and performs all rendering commands. This is the
same split used by Chromium/Dawn.

The engine acquires key `0`, renders with D3D12, transitions the slot to `COMMON`,
waits for its GPU fence, releases key `1`, then sends `FrameReady`. The managed
viewport calls `CompositionDrawingSurface.UpdateWithKeyedMutexAsync(image, 1, 0)`
and sends `FrameAck` when the copy completes. The engine only reuses a slot after
that ack.

The pipe is a control channel only. Raw `HANDLE` values are process-local; the
managed side calls `DuplicateHandle` for each `HelloEngine` handle before passing
it to Avalonia. No pixel data is sent through the pipe.

## Editor mode

`Engine.exe --editor --pipe=<path> --width=<w> --height=<h> --fps=<n>` runs
headless: no native window and no swap chain. Native ImGui still renders into the
shared texture; the GLFW ImGui backend is skipped in this mode.

The Avalonia HWND is not passed to the engine. The renderer does not need it for
D3D12 device creation, offscreen rendering, or the shared-texture path.
