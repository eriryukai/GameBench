// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Application.h"
#include "InteropNamePipeClient.h"
#include "OS/Window/Window.h"
#include "Renderer/Renderer.h"
#include "Renderer/RendererFactory.h"

#include <chrono>

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
	const auto StartupBegin = std::chrono::steady_clock::now();

	// In editor mode the handshake comes first: every step after it reports
	// progress to the splash screen, and a failed handshake aborts startup
	// instead of opening a window nobody will look at.
	if (IsEditorMode())
	{
		m_IpcClient = std::make_unique<InteropNamePipeClient>();

		ReportProgress("connect", "Connecting to editor...", 1, 3);

		std::string ConnectError;
		if (!m_IpcClient->ConnectAndHandshake(m_Specification.IpcPipeName, 15000, ConnectError))
		{
			m_LastError = ConnectError;
			m_IpcClient.reset();
			return 1;
		}
	}

	ReportProgress("window", "Creating window...", 2, 3);

	WindowSpecification WindowSpec = m_Specification.Window;
	WindowSpec.SetPlatformData(PlatformData);

	m_Window = std::make_unique<Window>();
	if (!m_Window->Create(WindowSpec))
	{
		m_LastError = "Could not create the application window.";
		m_Window.reset();
		if (m_IpcClient)
		{
			m_IpcClient->SendLog("error", m_LastError);
		}
		return 1;
	}

	ReportProgress("renderer", "Initializing renderer...", 3, 3);

	m_Renderer = std::make_unique<Renderer>();
	m_Renderer->Initialize(m_Window.get());
	if (!m_Renderer->IsInitialized())
	{
		m_LastError = "Could not initialize the renderer.";
		m_Renderer.reset();
		m_Window.reset();
		if (m_IpcClient)
		{
			m_IpcClient->SendLog("error", m_LastError);
		}
		return 1;
	}

	m_Renderer->GetFactory()->SetVSyncEnabled(m_Specification.VSync);

	m_Initialized = true;
	m_Running = true;

	if (m_IpcClient)
	{
		const auto ElapsedMs = std::chrono::duration_cast<std::chrono::milliseconds>(
			std::chrono::steady_clock::now() - StartupBegin).count();
		m_IpcClient->SendReady(ElapsedMs);
	}

	return 0;
}

void Application::Tick()
{
	if (!m_Initialized)
	{
		return;
	}

	// Editor commands arrive on the pipe; drain them here so gameplay code stays
	// single threaded. Shutdown is the only command in this slice.
	if (m_IpcClient)
	{
		DrainInteropMessages();
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
		if (!m_Renderer->IsInitialized())
		{
			return;
		}

		uint32_t width = 0, height = 0;
		m_Window->PollFramebufferSize(width, height);
		if (width == 0 || height == 0)
		{
			// Minimized or occluded: idle instead of spinning a 100% CPU loop.
			m_Window->WaitEventsTimeout(0.05);
			return;
		}

		if (m_Renderer->GetFactory()->GetWindowDimensionsWidth() != width ||
			m_Renderer->GetFactory()->GetWindowDimensionsHeight() != height)
		{
			m_Renderer->GetFactory()->SetWindowDimensions(width, height);
			m_Renderer->GetFactory()->ResizeSwapChain();
		}

		m_Renderer->BeginRender();
		// @TODO: DrawCalls from game simulation to renderer.
		if (GetSpecification().bEnabledImGui)
		{
			m_Renderer->RenderImGui();
		}
		m_Renderer->EndRender();
		m_Renderer->Present();
	}
}

void Application::Shutdown()
{
	if (!m_Initialized)
	{
		return;
	}

	// Closing the pipe first lets the editor notice the teardown immediately.
	m_IpcClient.reset();

	m_Renderer.reset();
	m_Window.reset();

	for (Layer* layer : m_LayerStack)
	{
		layer->OnDetach();
		delete layer;
	}
	m_LayerStack.Clear();

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

void Application::ReportProgress(const std::string& Step, const std::string& Message, int32_t Index, int32_t Total)
{
	if (m_IpcClient)
	{
		m_IpcClient->SendProgress(Step, Message, Index, Total);
	}
}

void Application::DrainInteropMessages()
{
	Interop::FIncomingMessage Message;
	while (m_IpcClient->TryPopMessage(Message))
	{
		switch (Message.Type)
		{
		case Interop::EMessageType::Shutdown:
			Close();
			break;
		default:
			// LoadProject / Play / Stop arrive here in later slices.
			break;
		}
	}

	if (m_IpcClient->IsHostGone())
	{
		std::print("[GameBench] Editor connection lost; shutting down.\n");
		Close();
	}
}
