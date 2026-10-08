// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Application.h"
#include "OS/Window/Window.h"
#include "Renderer/Renderer.h"
#include <iostream>

Application* Application::s_Instance = nullptr;

Application::Application(const ApplicationSpecification& specification)
	: m_Specification(specification)
{
	s_Instance = this;
}

Application::~Application()
{
	Shutdown();

	if (s_Instance == this)
	{
		s_Instance = nullptr;
	}
}

int Application::Initialize(void* PlatformData)
{
	if (IsInitialized())
	{
		return 0;
	}

	m_LastError.clear();

	WindowSpecification WindowSpec = m_Specification.Window;
	WindowSpec.SetPlatformData(PlatformData);

	m_Window = std::make_unique<Window>();
	if (!m_Window->Create(WindowSpec))
	{
		m_LastError = "Could not create the application window.";
		m_Window.reset();
		return 1;
	}

	m_Renderer = std::make_unique<Renderer>();
	m_Renderer->SetVSyncEnabled(m_Specification.VSync);
	m_Renderer->Initialize(m_Window.get());
	if (!m_Renderer->IsInitialized())
	{
		m_LastError = "Could not initialize the renderer.";
		m_Renderer.reset();
		m_Window.reset();
		return 1;
	}

	m_Initialized = true;
	m_Running = true;

	return 0;
}

void Application::Tick()
{
	if (!m_Initialized)
	{
		return;
	}

	if (m_Window)
	{
		m_Window->PumpMessages();
		if (m_Window->ShouldClose())
		{
			Close();
		}
	}

	for (Layer* layer : m_LayerStack)
	{
		layer->OnUpdate();
	}

	if (m_Renderer)
	{
		m_Renderer->Tick();
	}

	std::cout << "[C++ Client] Connecting to pipe...\n";

	// 1. Connect to the pipe using the absolute Win32 pipe namespace path
	HANDLE hPipe = CreateFileW(
		L"\\\\.\\pipe\\MyTestPipe",   // Pipe name matching C#
		GENERIC_READ | GENERIC_WRITE, // Read and write access
		0,                            // No sharing 
		NULL,                         // Default security attributes
		OPEN_EXISTING,                // Opens an existing pipe
		0,                            // Default attributes
		NULL                          // No template file
	);

	if (hPipe == INVALID_HANDLE_VALUE) {
		std::cerr << "[C++ Client] Failed to connect. Error: " << GetLastError() << "\n";
		return;
	}
	std::cout << "[C++ Client] Connected successfully!\n";

	// 2. Write a message to the C# Server (Include '\n' since C# uses ReadLine)
	std::string message = "Hello from your C++ Client!\n";
	DWORD bytesWritten = 0;

	BOOL isSuccess = WriteFile(
		hPipe,
		message.c_str(),
		static_cast<DWORD>(message.length()),
		&bytesWritten,
		NULL
	);

	if (!isSuccess) {
		std::cerr << "[C++ Client] WriteFile failed. Error: " << GetLastError() << "\n";
		CloseHandle(hPipe);
		return;
	}
	std::cout << "[C++ Client] Message sent.\n";

	// 3. Read the server's reply
	char buffer[512] = { 0 };
	DWORD bytesRead = 0;

	isSuccess = ReadFile(
		hPipe,
		buffer,
		sizeof(buffer) - 1,
		&bytesRead,
		NULL
	);

	if (isSuccess && bytesRead > 0) {
		std::cout << "[C++ Client] Received: " << buffer;
	}
	else {
		std::cerr << "[C++ Client] ReadFile failed. Error: " << GetLastError() << "\n";
	}

	// 4. Clean up resources
	CloseHandle(hPipe);
}

void Application::Shutdown()
{
	if (!m_Initialized)
	{
		return;
	}

	for (Layer* layer : m_LayerStack)
	{
		layer->OnDetach();
		delete layer;
	}
	m_LayerStack.Clear();

	// The renderer borrows the window, so it has to go first.
	m_Renderer.reset();
	m_Window.reset();

	m_Initialized = false;
}

void Application::Run(void* PlatformData)
{
	if (Initialize(PlatformData) != 0)
	{
		std::print("Application::Run aborted: {}", m_LastError);
		return;
	}

	while (m_Running)
	{
		Tick();
	}

	Shutdown();
}

void Application::Close()
{
	m_Running = false;
}

void Application::PushLayer(Layer* layer)
{
	if (!layer)
	{
		return;
	}

	m_LayerStack.PushLayer(layer);
	layer->OnAttach();
}

void Application::PushOverlay(Layer* layer)
{
	if (!layer)
	{
		return;
	}

	m_LayerStack.PushOverlay(layer);
	layer->OnAttach();
}

void Application::PopLayer(Layer* layer)
{
	if (!layer)
	{
		return;
	}

	m_LayerStack.PopLayer(layer);
	layer->OnDetach();
}

void Application::PopOverlay(Layer* layer)
{
	if (!layer)
	{
		return;
	}

	m_LayerStack.PopOverlay(layer);
	layer->OnDetach();
}
