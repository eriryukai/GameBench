// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Renderer/Viewport/ViewportInput.h"

#include <cstdint>

// Wire protocol shared by the native engine and the managed editor.
// Keep this file layout-only: it is included by both the IPC implementation and
// mirrored by Editor/Source/Native/EditorIpc/EditorProtocol.cs.
namespace EditorProtocol
{
	constexpr uint32_t Magic = 0x4D545250; // 'MTRP'
	constexpr uint16_t Version = 1;
	constexpr uint32_t MaxSlots = 3;
	constexpr uint32_t MaxPayloadSize = 1024 * 1024;

	enum class MessageType : uint16_t
	{
		HelloEditor = 1,
		HelloEngine = 2,
		FrameReady = 3,
		FrameAck = 4,
		Resize = 5,
		Input = 6,
		Shutdown = 7,
	};

#pragma pack(push, 1)
	struct MessageHeader
	{
		uint32_t MagicValue = Magic;
		uint16_t ProtocolVersion = Version;
		uint16_t Type = 0;
		uint32_t PayloadSize = 0;
		uint32_t Reserved = 0;
		uint64_t FrameId = 0;
	};

	struct HelloEditor
	{
		uint32_t ProcessId = 0;
		uint32_t Reserved = 0;
	};

	struct HelloEngine
	{
		uint32_t ProcessId = 0;
		uint32_t Width = 0;
		uint32_t Height = 0;
		uint32_t Format = 0; // DXGI_FORMAT
		uint32_t SlotCount = 0;
		uint32_t Reserved = 0;
		uint64_t AdapterLuid = 0;
		uint64_t SharedHandles[MaxSlots] = {};
	};

	struct FrameReady
	{
		uint32_t Slot = 0;
		uint32_t Reserved = 0;
	};

	struct FrameAck
	{
		uint32_t Slot = 0;
		uint32_t Reserved = 0;
		uint64_t FrameId = 0;
	};

	struct Resize
	{
		uint32_t Width = 0;
		uint32_t Height = 0;
	};

	using Input = ::ViewportInputEvent;

	struct Shutdown
	{
		uint32_t Reason = 0;
		uint32_t Reserved = 0;
	};
#pragma pack(pop)

	static_assert(sizeof(MessageHeader) == 24, "EditorProtocol header layout changed");
	static_assert(sizeof(HelloEngine) == 56, "EditorProtocol hello layout changed");
	static_assert(sizeof(FrameAck) == 16, "EditorProtocol ack layout changed");
}
