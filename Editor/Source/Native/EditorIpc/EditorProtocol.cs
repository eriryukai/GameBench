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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ShutdownPayload
{
    public uint Reason;
    public uint Reserved;
}
