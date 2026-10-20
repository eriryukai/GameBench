// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System.Text.Json.Serialization;

namespace GameBench.Editor.Native.Interop;

// Frame type byte of the editor <-> engine pipe protocol. Keep in sync with
// Engine/Source/Interop/InteropProtocol.h.
internal enum InteropMessageType : byte
{
    Hello = 1,
    HelloAck = 2,
    Reject = 3,
    Progress = 4,
    Log = 5,
    Ready = 6,
    Shutdown = 7,
}

internal static class InteropProtocol
{
    public const int Version = 1;
}

public sealed class HelloMessage
{
    [JsonPropertyName("protocol")] public int Protocol { get; init; }
    [JsonPropertyName("pid")] public int Pid { get; init; }
    [JsonPropertyName("mode")] public string Mode { get; init; } = string.Empty;
    [JsonPropertyName("engineVersion")] public string EngineVersion { get; init; } = string.Empty;
}

internal sealed class HelloAckMessage
{
    [JsonPropertyName("protocol")] public int Protocol { get; init; }
    [JsonPropertyName("sessionId")] public string SessionId { get; init; } = string.Empty;
}

internal sealed class RejectMessage
{
    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;
}

internal sealed class ProgressMessage
{
    [JsonPropertyName("step")] public string Step { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}

internal sealed class LogMessage
{
    [JsonPropertyName("level")] public string Level { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

internal sealed class ReadyMessage
{
    [JsonPropertyName("elapsedMs")] public long ElapsedMs { get; init; }
}
