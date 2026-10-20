// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <mutex>
#include <string>
#include <thread>

#include "InteropProtocol.h"

// The engine-side half of the editor IPC. Engine.exe connects to the pipe the
// editor passed via --ipc-pipe=<name>, performs the Hello handshake and then:
//   - streams progress / log / ready frames while it initializes,
//   - queues frames coming the other way for Application::Tick to drain.
//
// The pipe handle is opened overlapped: Windows serializes synchronous I/O on
// a single handle, so a blocking reader would also block the writer. All reads
// run on one background thread, all writes on the caller's thread; both use
// OVERLAPPED operations with their own event.
//
// When the host closes the pipe the client flags IsHostGone() so the
// application can exit instead of rendering into the void.
class InteropNamePipeClient
{
public:
	InteropNamePipeClient() = default;
	~InteropNamePipeClient();

	InteropNamePipeClient(const InteropNamePipeClient&) = delete;
	InteropNamePipeClient& operator=(const InteropNamePipeClient&) = delete;

	// Connects to \\.\pipe\<PipeName> within TimeoutMs, sends Hello and waits
	// for HelloAck. Returns false and fills OutError when the host cannot be
	// reached or rejects the handshake.
	bool ConnectAndHandshake(const std::string& PipeName, uint32_t TimeoutMs, std::string& OutError);

	// Closes the pipe and joins the reader thread. Safe to call more than once.
	void Disconnect();

	bool IsConnected() const { return m_Connected.load(); }

	// Set when the pipe breaks after a successful handshake, or when a write
	// fails. The application treats it as "the editor is gone, exit cleanly".
	bool IsHostGone() const { return m_HostGone.load(); }

	void SendProgress(const std::string& Step, const std::string& Message, int32_t Index, int32_t Total);
	void SendLog(const std::string& Level, const std::string& Message);
	void SendReady(int64_t ElapsedMs);

	// Drains one queued host -> engine frame. Returns false when the queue is empty.
	bool TryPopMessage(Interop::FIncomingMessage& OutMessage);

private:
	enum class EHandshakeState
	{
		Pending,
		Acknowledged,
		Rejected,
		Failed,
	};

	void ReaderLoop();
	void SignalHandshake(EHandshakeState State, const std::string& Detail);

	bool ReadFrame(Interop::EMessageType& OutType, std::string& OutJson);
	bool ReadExact(void* OutBuffer, uint32_t Size);
	bool WriteFrame(Interop::EMessageType Type, const std::string& Json);
	bool WriteAll(const void* Data, uint32_t Size);

	void* m_Pipe = nullptr;       // HANDLE
	void* m_ReadEvent = nullptr;  // HANDLE, manual reset, owned by the reader thread
	void* m_WriteEvent = nullptr; // HANDLE, manual reset, guarded by m_WriteMutex
	std::thread m_ReaderThread;

	std::atomic<bool> m_Connected{ false };
	std::atomic<bool> m_HostGone{ false };
	std::atomic<bool> m_Disconnecting{ false };

	EHandshakeState m_HandshakeState = EHandshakeState::Pending;
	std::string m_HandshakeDetail;
	std::mutex m_HandshakeMutex;
	std::condition_variable m_HandshakeCv;

	std::deque<Interop::FIncomingMessage> m_Incoming;
	std::mutex m_IncomingMutex;

	std::mutex m_WriteMutex;
};
