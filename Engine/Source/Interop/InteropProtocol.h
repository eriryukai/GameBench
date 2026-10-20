// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <cstdint>
#include <string>

// Wire protocol between Editor.exe (host, pipe server) and Engine.exe (client).
//
// Every frame is:
//     uint32 length   little-endian; number of bytes that follow (type + payload)
//     uint8  type     Interop::EMessageType
//     bytes  payload  UTF-8 JSON (length - 1 bytes)
//
// Payloads per message:
//     Hello      Engine -> Editor  { "protocol": 1, "pid": ..., "mode": "editor", "engineVersion": "0.1.0" }
//     HelloAck   Editor -> Engine  { "protocol": 1, "sessionId": "..." }
//     Reject     Editor -> Engine  { "reason": "..." }
//     Progress   Engine -> Editor  { "step": "...", "message": "...", "index": 1, "total": 3 }
//     Log        Engine -> Editor  { "level": "info", "message": "..." }
//     Ready      Engine -> Editor  { "elapsedMs": 123 }
//     Shutdown   Editor -> Engine  { }
//
// Bump ProtocolVersion whenever a message shape changes; the editor rejects
// mismatches instead of talking past each other.
namespace Interop
{
	inline constexpr uint32_t ProtocolVersion = 1;
	inline constexpr const char* EngineVersion = "0.1.0";
	inline constexpr uint32_t MaxFrameSize = 4 * 1024 * 1024;

	enum class EMessageType : uint8_t
	{
		Hello = 1,
		HelloAck = 2,
		Reject = 3,
		Progress = 4,
		Log = 5,
		Ready = 6,
		Shutdown = 7,
	};

	// A decoded frame. Application::Tick drains these for host -> engine commands.
	struct FIncomingMessage
	{
		EMessageType Type = EMessageType::Shutdown;
		std::string Json;
	};
}
