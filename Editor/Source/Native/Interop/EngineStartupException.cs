// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;

namespace GameBench.Editor.Native.Interop;

// Raised when the engine cannot be spawned, connected or handshaken. The
// message is written for the splash screen, not for a log file.
public sealed class EngineStartupException : Exception
{
    public EngineStartupException(string message) : base(message)
    {
    }
}
