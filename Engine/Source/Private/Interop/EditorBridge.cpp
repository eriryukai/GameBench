// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Interop/EditorBridge.h"

#include <algorithm>
#include <cstring>
#include <print>

namespace
{
	std::wstring Utf8ToWide(const std::string& Value)
	{
		if (Value.empty())
		{
			return {};
		}

		const int Length = MultiByteToWideChar(CP_UTF8, 0, Value.c_str(), static_cast<int>(Value.size()), nullptr, 0);
		if (Length <= 0)
		{
			return {};
		}

		std::wstring Result(static_cast<size_t>(Length), L'\0');
		MultiByteToWideChar(CP_UTF8, 0, Value.c_str(), static_cast<int>(Value.size()), Result.data(), Length);
		return Result;
	}

	bool ReadAll(HANDLE Pipe, HANDLE Event, void* Data, size_t Size)
	{
		auto* Cursor = static_cast<uint8_t*>(Data);
		size_t Remaining = Size;

		while (Remaining > 0)
		{
			OVERLAPPED Overlapped = {};
			Overlapped.hEvent = Event;
			ResetEvent(Event);

			DWORD Read = 0;
			const BOOL bStarted = ReadFile(
				Pipe, Cursor, static_cast<DWORD>(Remaining), &Read, &Overlapped);

			if (!bStarted)
			{
				const DWORD Error = GetLastError();
				if (Error != ERROR_IO_PENDING)
				{
					return false;
				}

				if (WaitForSingleObject(Event, INFINITE) != WAIT_OBJECT_0)
				{
					return false;
				}
			}

			if (!GetOverlappedResult(Pipe, &Overlapped, &Read, FALSE) || Read == 0)
			{
				return false;
			}

			Cursor += Read;
			Remaining -= Read;
		}

		return true;
	}
}

EditorBridge::~EditorBridge()
{
	Shutdown();
}

bool EditorBridge::Initialize(
	ID3D12Device* Device,
	const std::string& InPipePath,
	uint32_t Width,
	uint32_t Height,
	uint64_t InAdapterLuid)
{
	Shutdown();
	bStop = false;
	bShouldExit = false;

	if (!Device || InPipePath.empty())
	{
		return false;
	}

	PipePath = InPipePath;
	AdapterLuid = InAdapterLuid;

	FrameRing = std::make_unique<ViewportFrameRing>();
	if (!FrameRing->Initialize(Device, Width, Height))
	{
		FrameRing.reset();
		return false;
	}
	if (!ConnectPipe())
	{
		std::print("[EditorBridge] Could not connect to editor pipe '{}'.\n", PipePath);
		FrameRing->Shutdown();
		FrameRing.reset();
		return false;
	}

	bStop = false;
	bShouldExit = false;
	bConnected = true;

	EditorProtocol::HelloEngine Hello = {};
	Hello.ProcessId = GetCurrentProcessId();
	Hello.Width = FrameRing->GetWidth();
	Hello.Height = FrameRing->GetHeight();
	Hello.Format = static_cast<uint32_t>(FrameRing->GetFormat());
	Hello.SlotCount = ViewportFrameRing::SlotCount;
	Hello.AdapterLuid = AdapterLuid;

	for (uint32_t Index = 0; Index < ViewportFrameRing::SlotCount; ++Index)
	{
		const auto& Slot = FrameRing->GetSlot(Index);
		Hello.SharedHandles[Index] = static_cast<uint64_t>(reinterpret_cast<uintptr_t>(Slot.SharedHandle));
	}

	SendMessage(EditorProtocol::MessageType::HelloEngine, &Hello, sizeof(Hello));
	Writer = std::thread(&EditorBridge::WriterThread, this);
	Reader = std::thread(&EditorBridge::ReaderThread, this);
	return true;
}

void EditorBridge::Shutdown()
{
	if (bStop.exchange(true))
	{
		return;
	}

	bConnected = false;

	if (Pipe != INVALID_HANDLE_VALUE)
	{
		CancelIoEx(Pipe, nullptr);
	}

	QueueCondition.notify_all();

	// The pipe was opened for synchronous I/O. CancelIoEx cancels pending
	// overlapped operations, while CancelSynchronousIo is what actually
	// releases a reader blocked in ReadFile.
	if (Reader.joinable())
	{
		CancelSynchronousIo(Reader.native_handle());
	}
	if (Writer.joinable())
	{
		CancelSynchronousIo(Writer.native_handle());
	}

	if (Reader.joinable())
	{
		Reader.join();
	}
	if (Writer.joinable())
	{
		Writer.join();
	}

	if (Pipe != INVALID_HANDLE_VALUE)
	{
		CloseHandle(Pipe);
		Pipe = INVALID_HANDLE_VALUE;
	}

	if (ReadEvent)
	{
		CloseHandle(ReadEvent);
		ReadEvent = nullptr;
	}
	if (WriteEvent)
	{
		CloseHandle(WriteEvent);
		WriteEvent = nullptr;
	}

	{
		std::lock_guard<std::mutex> Lock(QueueMutex);
		Outbound.clear();
	}

	if (FrameRing)
	{
		FrameRing->Shutdown();
		FrameRing.reset();
	}
}

void EditorBridge::Tick()
{
	// The reader thread handles acknowledgements and shutdown directly. This hook
	// exists so Application can keep bridge work on the main thread later (e.g.
	// resize handshakes) without changing its tick structure.
}

uint32_t EditorBridge::GetViewportWidth() const
{
	return FrameRing ? FrameRing->GetWidth() : 0;
}

uint32_t EditorBridge::GetViewportHeight() const
{
	return FrameRing ? FrameRing->GetHeight() : 0;
}

int EditorBridge::AcquireWritableSlot()
{
	return FrameRing ? FrameRing->AcquireWritableSlot() : -1;
}

void EditorBridge::PublishFrame(uint32_t SlotIndex)
{
	if (!FrameRing || !bConnected.load())
	{
		return;
	}

	const uint64_t FrameId = ++NextFrameId;
	FrameRing->PublishSlot(SlotIndex, FrameId);

	EditorProtocol::FrameReady Ready = {};
	Ready.Slot = SlotIndex;
	SendMessage(EditorProtocol::MessageType::FrameReady, &Ready, sizeof(Ready), FrameId);
}

bool EditorBridge::ConnectPipe()
{
	const std::wstring WidePath = Utf8ToWide(PipePath);
	if (WidePath.empty())
	{
		return false;
	}

	for (int Attempt = 0; Attempt < 100 && !bStop.load(); ++Attempt)
	{
		Pipe = CreateFileW(
			WidePath.c_str(),
			GENERIC_READ | GENERIC_WRITE,
			0,
			nullptr,
			OPEN_EXISTING,
			FILE_FLAG_OVERLAPPED,
			nullptr);

		if (Pipe != INVALID_HANDLE_VALUE)
		{
			DWORD Mode = PIPE_READMODE_BYTE;
			SetNamedPipeHandleState(Pipe, &Mode, nullptr, nullptr);

			ReadEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
			WriteEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
			return ReadEvent != nullptr && WriteEvent != nullptr;
		}

		Sleep(50);
	}

	return false;
}

void EditorBridge::WriterThread()
{
	while (!bStop.load())
	{
		OutboundMessage Message;
		{
			std::unique_lock<std::mutex> Lock(QueueMutex);
			QueueCondition.wait(Lock, [this]
			{
				return bStop.load() || !Outbound.empty();
			});

			if (bStop.load() && Outbound.empty())
			{
				return;
			}

			Message = std::move(Outbound.front());
			Outbound.pop_front();
		}

		if (!Message.Bytes.empty() && !WriteAll(Message.Bytes.data(), Message.Bytes.size()))
		{
			SetDisconnected();
			return;
		}
	}
}

void EditorBridge::ReaderThread()
{
	std::vector<uint8_t> Payload;

	while (!bStop.load())
	{
		EditorProtocol::MessageHeader Header = {};
		if (!ReadAll(Pipe, ReadEvent, &Header, sizeof(Header)))
		{
			SetDisconnected();
			return;
		}

		if (Header.MagicValue != EditorProtocol::Magic ||
			Header.ProtocolVersion != EditorProtocol::Version ||
			Header.PayloadSize > EditorProtocol::MaxPayloadSize)
		{
			std::print("[EditorBridge] Invalid editor message header.\n");
			SetDisconnected();
			return;
		}

		Payload.resize(Header.PayloadSize);
		if (Header.PayloadSize > 0 && !ReadAll(Pipe, ReadEvent, Payload.data(), Payload.size()))
		{
			SetDisconnected();
			return;
		}

		HandleMessage(Header, Payload);
	}
}

bool EditorBridge::SendMessage(EditorProtocol::MessageType Type, const void* Payload, uint32_t PayloadSize, uint64_t FrameId)
{
	if (PayloadSize > EditorProtocol::MaxPayloadSize)
	{
		return false;
	}

	EditorProtocol::MessageHeader Header = {};
	Header.Type = static_cast<uint16_t>(Type);
	Header.PayloadSize = PayloadSize;
	Header.FrameId = FrameId;

	OutboundMessage Message;
	Message.Bytes.resize(sizeof(Header) + PayloadSize);
	std::memcpy(Message.Bytes.data(), &Header, sizeof(Header));
	if (PayloadSize > 0 && Payload)
	{
		std::memcpy(Message.Bytes.data() + sizeof(Header), Payload, PayloadSize);
	}

	{
		std::lock_guard<std::mutex> Lock(QueueMutex);
		if (Outbound.size() >= 64)
		{
			// The editor is not draining. Drop only frame notifications; state
			// messages are small and must not be silently discarded.
			if (Type != EditorProtocol::MessageType::FrameReady)
			{
				return false;
			}

			Outbound.pop_front();
		}
		Outbound.push_back(std::move(Message));
	}

	QueueCondition.notify_one();
	return true;
}

bool EditorBridge::WriteAll(const void* Data, size_t Size)
{
	std::lock_guard<std::mutex> Lock(WriteMutex);

	auto* Cursor = static_cast<const uint8_t*>(Data);
	size_t Remaining = Size;

	while (Remaining > 0)
	{
		OVERLAPPED Overlapped = {};
		Overlapped.hEvent = WriteEvent;
		ResetEvent(WriteEvent);

		DWORD Written = 0;
		const BOOL bStarted = WriteFile(
			Pipe, Cursor, static_cast<DWORD>(Remaining), &Written, &Overlapped);

		if (!bStarted)
		{
			const DWORD Error = GetLastError();
			if (Error != ERROR_IO_PENDING)
			{
				return false;
			}

			if (WaitForSingleObject(WriteEvent, INFINITE) != WAIT_OBJECT_0)
			{
				return false;
			}
		}

		if (!GetOverlappedResult(Pipe, &Overlapped, &Written, FALSE) || Written == 0)
		{
			return false;
		}

		Cursor += Written;
		Remaining -= Written;
	}

	return true;
}

void EditorBridge::HandleMessage(const EditorProtocol::MessageHeader& Header, const std::vector<uint8_t>& Payload)
{
	switch (static_cast<EditorProtocol::MessageType>(Header.Type))
	{
	case EditorProtocol::MessageType::FrameAck:
	{
		if (Payload.size() < sizeof(EditorProtocol::FrameAck) || !FrameRing)
		{
			return;
		}

		EditorProtocol::FrameAck Ack = {};
		std::memcpy(&Ack, Payload.data(), sizeof(Ack));
		FrameRing->Acknowledge(Ack.Slot, Ack.FrameId);
		break;
	}
	case EditorProtocol::MessageType::Shutdown:
		bShouldExit = true;
		break;
	case EditorProtocol::MessageType::Resize:
		// Resizing the shared ring is intentionally deferred until the first
		// viewport path is proven. The managed viewport stretches the current
		// texture while the window size changes.
		break;
	case EditorProtocol::MessageType::Input:
		// Display-only native ImGui for now; input forwarding comes later.
		break;
	default:
		break;
	}
}

void EditorBridge::SetDisconnected()
{
	if (bConnected.exchange(false))
	{
		std::print("[EditorBridge] Editor disconnected.\n");
	}

	// The editor owns the engine process. If the pipe dies, the engine should not
	// keep rendering into a texture nobody can display.
	bShouldExit = true;
}
