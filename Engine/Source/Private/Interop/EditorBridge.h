// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Interop/EditorProtocol.h"
#include "Renderer/Viewport/ViewportFrameRing.h"

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

// Native side of the editor control channel. The named pipe is only a control
// plane: shared texture NT handles are duplicated into the editor process by the
// managed side, and frames are synchronized by the three-slot ring + acks.
class EditorBridge
{
public:
	EditorBridge() = default;
	~EditorBridge();

	EditorBridge(const EditorBridge&) = delete;
	EditorBridge& operator=(const EditorBridge&) = delete;

	bool Initialize(
		ID3D12Device* Device,
		const std::string& PipePath,
		uint32_t Width,
		uint32_t Height,
		uint64_t AdapterLuid);

	void Shutdown();
	void Tick();

	bool IsConnected() const { return bConnected.load(); }
	bool ShouldExit() const { return bShouldExit.load(); }

	uint32_t GetViewportWidth() const;
	uint32_t GetViewportHeight() const;

	int AcquireWritableSlot();
	void PublishFrame(uint32_t SlotIndex);
	ViewportFrameRing* GetFrameRing() { return FrameRing.get(); }

	// Called on the render thread before ImGui::NewFrame. The reader thread only
	// appends events here; it never touches ImGui state directly.
	void ConsumeInput(std::vector<ViewportInputEvent>& OutEvents);

private:
	struct OutboundMessage
	{
		std::vector<uint8_t> Bytes;
	};

	bool ConnectPipe();
	void WriterThread();
	void ReaderThread();
	bool SendMessage(EditorProtocol::MessageType Type, const void* Payload, uint32_t PayloadSize, uint64_t FrameId = 0);
	bool WriteAll(const void* Data, size_t Size);
	void HandleMessage(const EditorProtocol::MessageHeader& Header, const std::vector<uint8_t>& Payload);
	void SetDisconnected();

	std::unique_ptr<ViewportFrameRing> FrameRing;

	std::string PipePath;
	HANDLE Pipe = INVALID_HANDLE_VALUE;
	HANDLE ReadEvent = nullptr;
	HANDLE WriteEvent = nullptr;
	uint64_t AdapterLuid = 0;

	std::thread Writer;
	std::thread Reader;
	std::mutex QueueMutex;
	std::condition_variable QueueCondition;
	std::deque<OutboundMessage> Outbound;
	std::mutex WriteMutex;

	std::mutex InputMutex;
	std::vector<ViewportInputEvent> PendingInput;

	std::atomic<bool> bStop = false;
	std::atomic<bool> bConnected = false;
	std::atomic<bool> bShouldExit = false;

	uint64_t NextFrameId = 0;
};
