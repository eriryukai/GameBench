// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "InteropNamePipeClient.h"

#include <nlohmann/json.hpp>

#include <chrono>
#include <cstring>

namespace
{
	constexpr uint32_t ConnectRetrySleepMs = 100;
	constexpr uint32_t ConnectWaitNamedPipeMs = 250;
	constexpr uint32_t HandshakeTimeoutMs = 5000;

	// Overlapped reads wait in slices so a shutdown (CancelIoEx) is noticed promptly.
	constexpr DWORD IoWaitSliceMs = 100;

	std::wstring ToWide(const std::string& Text)
	{
		if (Text.empty())
		{
			return {};
		}

		const int Size = MultiByteToWideChar(CP_UTF8, 0, Text.c_str(), static_cast<int>(Text.size()), nullptr, 0);
		std::wstring Wide(static_cast<size_t>(Size), L'\0');
		MultiByteToWideChar(CP_UTF8, 0, Text.c_str(), static_cast<int>(Text.size()), Wide.data(), Size);
		return Wide;
	}
}

InteropNamePipeClient::~InteropNamePipeClient()
{
	Disconnect();
}

bool InteropNamePipeClient::ConnectAndHandshake(const std::string& PipeName, uint32_t TimeoutMs, std::string& OutError)
{
	const std::wstring PipePath = L"\\\\.\\pipe\\" + ToWide(PipeName);
	const uint64_t Deadline = GetTickCount64() + TimeoutMs;

	HANDLE Pipe = INVALID_HANDLE_VALUE;
	while (GetTickCount64() < Deadline)
	{
		// Overlapped so a blocked reader never serializes the writer on this handle.
		Pipe = CreateFileW(
			PipePath.c_str(),
			GENERIC_READ | GENERIC_WRITE,
			0,
			nullptr,
			OPEN_EXISTING,
			FILE_FLAG_OVERLAPPED,
			nullptr);

		if (Pipe != INVALID_HANDLE_VALUE)
		{
			break;
		}

		// The editor creates the server before it spawns us, so FILE_NOT_FOUND
		// only means "not ready yet"; keep retrying inside the timeout window.
		const DWORD Error = GetLastError();
		if (Error == ERROR_PIPE_BUSY)
		{
			WaitNamedPipeW(PipePath.c_str(), ConnectWaitNamedPipeMs);
		}
		else if (Error != ERROR_FILE_NOT_FOUND)
		{
			OutError = "Could not open the editor pipe '" + PipeName + "' (error " + std::to_string(Error) + ").";
			return false;
		}

		std::this_thread::sleep_for(std::chrono::milliseconds(ConnectRetrySleepMs));
	}

	if (Pipe == INVALID_HANDLE_VALUE)
	{
		OutError = "Timed out connecting to the editor pipe '" + PipeName + "'.";
		return false;
	}

	m_Pipe = Pipe;
	m_ReadEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	m_WriteEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
	if (!m_ReadEvent || !m_WriteEvent)
	{
		OutError = "Failed to create the pipe I/O events.";
		Disconnect();
		return false;
	}

	const nlohmann::json Hello = {
		{ "protocol", Interop::ProtocolVersion },
		{ "pid", static_cast<uint32_t>(GetCurrentProcessId()) },
		{ "mode", "editor" },
		{ "engineVersion", Interop::EngineVersion },
	};

	if (!WriteFrame(Interop::EMessageType::Hello, Hello.dump()))
	{
		OutError = "Failed to send the Hello frame to the editor.";
		Disconnect();
		return false;
	}

	m_ReaderThread = std::thread(&InteropNamePipeClient::ReaderLoop, this);

	{
		std::unique_lock Lock(m_HandshakeMutex);
		if (!m_HandshakeCv.wait_for(Lock, std::chrono::milliseconds(HandshakeTimeoutMs),
			[this] { return m_HandshakeState != EHandshakeState::Pending; }))
		{
			OutError = "Timed out waiting for the editor to accept the handshake.";
			Disconnect();
			return false;
		}

		if (m_HandshakeState != EHandshakeState::Acknowledged)
		{
			OutError = m_HandshakeDetail.empty() ? "The editor rejected the handshake." : m_HandshakeDetail;
			Disconnect();
			return false;
		}
	}

	m_Connected = true;
	return true;
}

void InteropNamePipeClient::Disconnect()
{
	HANDLE Pipe = static_cast<HANDLE>(m_Pipe);
	m_Disconnecting = true;

	if (Pipe)
	{
		// Cancels the reader's pending read (and any in-flight write).
		CancelIoEx(Pipe, nullptr);
	}

	if (m_ReaderThread.joinable())
	{
		m_ReaderThread.join();
	}

	if (Pipe)
	{
		CloseHandle(Pipe);
	}
	m_Pipe = nullptr;

	if (m_ReadEvent)
	{
		CloseHandle(static_cast<HANDLE>(m_ReadEvent));
		m_ReadEvent = nullptr;
	}

	if (m_WriteEvent)
	{
		CloseHandle(static_cast<HANDLE>(m_WriteEvent));
		m_WriteEvent = nullptr;
	}

	m_Connected = false;
}

void InteropNamePipeClient::ReaderLoop()
{
	// The first frame is always the host's response to Hello.
	Interop::EMessageType Type = Interop::EMessageType::Shutdown;
	std::string Json;

	if (!ReadFrame(Type, Json))
	{
		SignalHandshake(EHandshakeState::Failed, "The editor connection closed during the handshake.");
		if (!m_Disconnecting)
		{
			m_HostGone = true;
		}
		return;
	}

	if (Type == Interop::EMessageType::HelloAck)
	{
		SignalHandshake(EHandshakeState::Acknowledged, {});
	}
	else if (Type == Interop::EMessageType::Reject)
	{
		std::string Reason = "The editor rejected the handshake.";
		try
		{
			Reason = nlohmann::json::parse(Json).value("reason", Reason);
		}
		catch (...)
		{
		}
		SignalHandshake(EHandshakeState::Rejected, Reason);
		return;
	}
	else
	{
		SignalHandshake(EHandshakeState::Failed, "The editor sent an unexpected handshake frame.");
		return;
	}

	// Steady state: queue frames until the host goes away.
	while (!m_Disconnecting.load())
	{
		if (!ReadFrame(Type, Json))
		{
			if (!m_Disconnecting)
			{
				m_Connected = false;
				m_HostGone = true;
			}
			return;
		}

		if (Type == Interop::EMessageType::Hello)
		{
			continue; // duplicated handshake frame; ignore
		}

		Interop::FIncomingMessage Message;
		Message.Type = Type;
		Message.Json = std::move(Json);

		{
			std::lock_guard Lock(m_IncomingMutex);
			m_Incoming.push_back(std::move(Message));
		}
	}
}

void InteropNamePipeClient::SignalHandshake(EHandshakeState State, const std::string& Detail)
{
	{
		std::lock_guard Lock(m_HandshakeMutex);
		m_HandshakeState = State;
		m_HandshakeDetail = Detail;
	}

	m_HandshakeCv.notify_all();
}

void InteropNamePipeClient::SendProgress(const std::string& Step, const std::string& Message, int32_t Index, int32_t Total)
{
	const nlohmann::json Payload = {
		{ "step", Step },
		{ "message", Message },
		{ "index", Index },
		{ "total", Total },
	};
	WriteFrame(Interop::EMessageType::Progress, Payload.dump());
}

void InteropNamePipeClient::SendLog(const std::string& Level, const std::string& Message)
{
	const nlohmann::json Payload = {
		{ "level", Level },
		{ "message", Message },
	};
	WriteFrame(Interop::EMessageType::Log, Payload.dump());
}

void InteropNamePipeClient::SendReady(int64_t ElapsedMs)
{
	const nlohmann::json Payload = { { "elapsedMs", ElapsedMs } };
	WriteFrame(Interop::EMessageType::Ready, Payload.dump());
}

bool InteropNamePipeClient::TryPopMessage(Interop::FIncomingMessage& OutMessage)
{
	std::lock_guard Lock(m_IncomingMutex);
	if (m_Incoming.empty())
	{
		return false;
	}

	OutMessage = std::move(m_Incoming.front());
	m_Incoming.pop_front();
	return true;
}

bool InteropNamePipeClient::ReadFrame(Interop::EMessageType& OutType, std::string& OutJson)
{
	uint32_t Length = 0;
	if (!ReadExact(&Length, sizeof(Length)))
	{
		return false;
	}

	if (Length == 0 || Length > Interop::MaxFrameSize)
	{
		return false;
	}

	std::string Payload;
	Payload.resize(Length);
	if (!ReadExact(Payload.data(), Length))
	{
		return false;
	}

	OutType = static_cast<Interop::EMessageType>(static_cast<uint8_t>(Payload[0]));
	OutJson.assign(Payload.begin() + 1, Payload.end());
	return true;
}

bool InteropNamePipeClient::ReadExact(void* OutBuffer, uint32_t Size)
{
	HANDLE Pipe = static_cast<HANDLE>(m_Pipe);
	HANDLE ReadEvent = static_cast<HANDLE>(m_ReadEvent);
	if (!Pipe || !ReadEvent)
	{
		return false;
	}

	uint8_t* Cursor = static_cast<uint8_t*>(OutBuffer);
	uint32_t Remaining = Size;

	while (Remaining > 0)
	{
		OVERLAPPED Overlapped = {};
		Overlapped.hEvent = ReadEvent;
		ResetEvent(ReadEvent);

		DWORD BytesRead = 0;
		if (!ReadFile(Pipe, Cursor, Remaining, &BytesRead, &Overlapped))
		{
			const DWORD Error = GetLastError();
			if (Error != ERROR_IO_PENDING)
			{
				return false;
			}

			while (true)
			{
				if (m_Disconnecting.load())
				{
					CancelIoEx(Pipe, &Overlapped);
					return false;
				}

				const DWORD Wait = WaitForSingleObject(ReadEvent, IoWaitSliceMs);
				if (Wait == WAIT_OBJECT_0)
				{
					break;
				}

				if (Wait == WAIT_FAILED)
				{
					return false;
				}
			}

			if (!GetOverlappedResult(Pipe, &Overlapped, &BytesRead, FALSE))
			{
				return false;
			}
		}

		if (BytesRead == 0)
		{
			return false; // host closed the pipe
		}

		Cursor += BytesRead;
		Remaining -= BytesRead;
	}

	return true;
}

bool InteropNamePipeClient::WriteFrame(Interop::EMessageType Type, const std::string& Json)
{
	if (!m_Pipe)
	{
		return false;
	}

	const uint32_t PayloadSize = static_cast<uint32_t>(1 + Json.size());
	const uint32_t FrameSize = static_cast<uint32_t>(sizeof(uint32_t) + PayloadSize);

	std::string Frame;
	Frame.resize(FrameSize);
	std::memcpy(Frame.data(), &PayloadSize, sizeof(PayloadSize));
	Frame[sizeof(uint32_t)] = static_cast<char>(Type);
	std::memcpy(Frame.data() + sizeof(uint32_t) + 1, Json.data(), Json.size());

	std::lock_guard Lock(m_WriteMutex);
	const bool bOk = WriteAll(Frame.data(), FrameSize);
	if (!bOk && m_Connected.load())
	{
		m_Connected = false;
		m_HostGone = true;
	}
	return bOk;
}

bool InteropNamePipeClient::WriteAll(const void* Data, uint32_t Size)
{
	HANDLE Pipe = static_cast<HANDLE>(m_Pipe);
	HANDLE WriteEvent = static_cast<HANDLE>(m_WriteEvent);
	if (!Pipe || !WriteEvent)
	{
		return false;
	}

	const uint8_t* Cursor = static_cast<const uint8_t*>(Data);
	uint32_t Remaining = Size;

	while (Remaining > 0)
	{
		OVERLAPPED Overlapped = {};
		Overlapped.hEvent = WriteEvent;
		ResetEvent(WriteEvent);

		DWORD BytesWritten = 0;
		if (!WriteFile(Pipe, Cursor, Remaining, &BytesWritten, &Overlapped))
		{
			const DWORD Error = GetLastError();
			if (Error != ERROR_IO_PENDING)
			{
				return false;
			}

			while (true)
			{
				if (m_Disconnecting.load())
				{
					CancelIoEx(Pipe, &Overlapped);
					return false;
				}

				const DWORD Wait = WaitForSingleObject(WriteEvent, IoWaitSliceMs);
				if (Wait == WAIT_OBJECT_0)
				{
					break;
				}

				if (Wait == WAIT_FAILED)
				{
					return false;
				}
			}

			if (!GetOverlappedResult(Pipe, &Overlapped, &BytesWritten, FALSE))
			{
				return false;
			}
		}

		if (BytesWritten == 0)
		{
			return false;
		}

		Cursor += BytesWritten;
		Remaining -= BytesWritten;
	}

	return true;
}
