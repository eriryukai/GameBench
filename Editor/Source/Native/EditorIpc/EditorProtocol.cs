// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Runtime.InteropServices;

namespace MortarEngine.Editor.Native.EditorIpc;

// Mirrors Engine/Source/Private/Interop/EditorProtocol.h. Keep Pack=1 and field
// order in sync; the native side validates the magic/version and message size.
internal static class EditorProtocol
{
    public const uint Magic = 0x4D545250; // 'MTRP'
    public const ushort Version = 1;
    public const uint MaxSlots = 3;
    public const uint MaxPayloadSize = 1024 * 1024;
    public const int HeaderSize = 24;

    public enum MessageType : ushort
    {
        HelloEditor = 1,
        HelloEngine = 2,
        FrameReady = 3,
        FrameAck = 4,
        Resize = 5,
        Input = 6,
        Shutdown = 7,
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct MessageHeader
{
    public uint Magic;
    public ushort ProtocolVersion;
    public ushort Type;
    public uint PayloadSize;
    public uint Reserved;
    public ulong FrameId;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct HelloEngine
{
    public uint ProcessId;
    public uint Width;
    public uint Height;
    public uint Format;
    public uint SlotCount;
    public uint Reserved;
    public ulong AdapterLuid;
    public ulong SharedHandle0;
    public ulong SharedHandle1;
    public ulong SharedHandle2;

    public ulong GetSharedHandle(int index) => index switch
    {
        0 => SharedHandle0,
        1 => SharedHandle1,
        2 => SharedHandle2,
        _ => 0,
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FrameReadyPayload
{
    public uint Slot;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FrameAckPayload
{
    public uint Slot;
    public uint Reserved;
    public ulong FrameId;
}

internal enum ViewportInputType : uint
{
    MouseMove = 0,
    MouseButton = 1,
    MouseWheel = 2,
    Key = 3,
    Text = 4,
    Focus = 5,
}

// Mirrors ViewportKey in Engine/Source/Public/Renderer/Viewport/ViewportInput.h.
internal enum ViewportKey : int
{
    None = 0,
    A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8, I = 9,
    J = 10, K = 11, L = 12, M = 13, N = 14, O = 15, P = 16, Q = 17,
    R = 18, S = 19, T = 20, U = 21, V = 22, W = 23, X = 24, Y = 25, Z = 26,
    D0 = 27, D1 = 28, D2 = 29, D3 = 30, D4 = 31,
    D5 = 32, D6 = 33, D7 = 34, D8 = 35, D9 = 36,
    Enter = 37,
    Escape = 38,
    Tab = 39,
    Backspace = 40,
    Space = 41,
    Left = 42,
    Right = 43,
    Up = 44,
    Down = 45,
    Home = 46,
    End = 47,
    PageUp = 48,
    PageDown = 49,
    Delete = 50,
    Insert = 51,
    LeftShift = 52,
    RightShift = 53,
    LeftCtrl = 54,
    RightCtrl = 55,
    LeftAlt = 56,
    RightAlt = 57,
    F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63,
    F7 = 64, F8 = 65, F9 = 66, F10 = 67, F11 = 68, F12 = 69,
    Comma = 70,
    Period = 71,
    Minus = 72,
    Equal = 73,
    Semicolon = 74,
    Slash = 75,
    Backslash = 76,
    Apostrophe = 77,
    Grave = 78,
    LeftBracket = 79,
    RightBracket = 80,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InputPayload
{
    public uint Kind;
    public float X;
    public float Y;
    public int Button;
    public int Down;
    public float Wheel;
    public int Key;
    public uint Modifiers;
    public uint Codepoint;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ShutdownPayload
{
    public uint Reason;
    public uint Reserved;
}
