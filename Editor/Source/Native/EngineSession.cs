#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GameBench.Editor.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct ViewportFrame
{
    public uint Width, Height, Format, Slot;
    public ulong Generation;
    public IntPtr ImageHandle;
}

// All calls are serialized by the viewport's gate and made on a worker thread.
internal sealed class EngineSession : SafeHandleZeroOrMinusOneIsInvalid
{
    private const string Library = "Engine.dll";
    private EngineSession() : base(true) { }

    public static EngineSession Create(byte[] luid)
    {
        if (luid.Length != 8)
            throw new InvalidOperationException("The compositor did not provide a valid GPU adapter LUID.");
        var session = new EngineSession();
        session.SetHandle(Engine_CreateEditorSession());
        if (session.IsInvalid)
            throw new InvalidOperationException(ReadError(IntPtr.Zero));
        try
        {
            session.Check(Engine_InitializeViewport(session, luid));
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public void Resize(uint width, uint height) => Check(Engine_ResetViewport(this, width, height));

    public ViewportFrame? Render()
    {
        int result = Engine_RenderViewport(this, out var frame);
        Check(result);
        return result == 2 ? null : frame;
    }

    private void Check(int result)
    {
        if (result == 0)
            throw new InvalidOperationException(ReadError(handle));
    }

    private static string ReadError(IntPtr session) =>
        Marshal.PtrToStringUTF8(Engine_GetEditorError(session)) ?? "Native renderer failure.";

    protected override bool ReleaseHandle()
    {
        Engine_DestroyEditorSession(handle);
        return true;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Engine_CreateEditorSession();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Engine_DestroyEditorSession(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Engine_GetEditorError(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Engine_InitializeViewport(EngineSession session, byte[] luid);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Engine_ResetViewport(EngineSession session, uint width, uint height);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Engine_RenderViewport(EngineSession session, out ViewportFrame frame);
}
