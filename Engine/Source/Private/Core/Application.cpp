// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Application.h"
#include "Interop/EditorBridge.h"
#include "OS/Window/Window.h"
#include "Renderer/Renderer.h"
#include "Renderer/RendererFactory.h"

#include <nfd.hpp>

#include <chrono>
#include <thread>

Application* Application::s_Instance = nullptr;

namespace
{
	LUID ToLuid(uint64_t Value)
	{
		LUID Result = {};
		Result.LowPart = static_cast<DWORD>(Value & 0xFFFFFFFFull);
		Result.HighPart = static_cast<LONG>(Value >> 32);
		return Result;
	}

	uint64_t FromLuid(const LUID& Value)
	{
		return (static_cast<uint64_t>(static_cast<uint32_t>(Value.HighPart)) << 32) |
			static_cast<uint64_t>(Value.LowPart);
	}
}

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
	LUID AdapterLuid = {};
	const LUID* AdapterLuidPtr = nullptr;
	if (m_Specification.bUseAdapterLuid)
	{
		AdapterLuid = ToLuid(m_Specification.AdapterLuid);
		AdapterLuidPtr = &AdapterLuid;
	}

	if (!m_Specification.bEditorMode)
	{
		WindowSpecification WindowSpec = m_Specification.Window;
		WindowSpec.SetPlatformData(PlatformData);

		m_Window = std::make_unique<Window>();
		if (!m_Window->Create(WindowSpec))
		{
			m_LastError = "Could not create the application window.";
			m_Window.reset();
			return 1;
		}
	}

	m_Renderer = std::make_unique<Renderer>();
	const bool bEnableImGui = m_Specification.bEnabledImGui && (m_Window != nullptr || m_Specification.bEditorMode);
	m_Renderer->Initialize(
		m_Window.get(),
		bEnableImGui,
		m_Specification.ViewportWidth,
		m_Specification.ViewportHeight,
		AdapterLuidPtr);
	if (!m_Renderer->IsInitialized())
	{
		m_LastError = "Could not initialize the renderer.";
		m_Renderer.reset();
		m_Window.reset();
		return 1;
	}

	m_Renderer->GetFactory()->SetVSyncEnabled(m_Specification.VSync);

	if (m_Specification.bEditorMode)
	{
		m_EditorBridge = std::make_unique<EditorBridge>();
		const bool bBridgeInitialized = m_EditorBridge->Initialize(
			m_Renderer->GetFactory()->GetDevice(),
			m_Specification.EditorPipeName,
			m_Specification.ViewportWidth,
			m_Specification.ViewportHeight,
			AdapterLuidPtr ? FromLuid(AdapterLuid) : 0);
		if (!bBridgeInitialized)
		{
			m_LastError = "Could not connect to the editor viewport pipe.";
			m_EditorBridge.reset();
			m_Renderer.reset();
			m_Window.reset();
			return 1;
		}
	}

	if (NFD::Init() != NFD_OKAY)
	{
		m_LastError = "Could not initialize file dialogs.";
		m_EditorBridge.reset();
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

	if (!m_Renderer || !m_Renderer->IsInitialized())
	{
		return;
	}

	if (m_Specification.bEditorMode)
	{
		if (!m_EditorBridge)
		{
			Close();
			return;
		}

		m_EditorBridge->Tick();

		if (m_EditorBridge->ShouldExit())
		{
			Close();
			return;
		}

		if (!m_EditorBridge->IsConnected())
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(10));
			return;
		}

		const uint32_t Width = m_EditorBridge->GetViewportWidth();
		const uint32_t Height = m_EditorBridge->GetViewportHeight();
		if (Width == 0 || Height == 0)
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(10));
			return;
		}

		const int SlotIndex = m_EditorBridge->AcquireWritableSlot();
		if (SlotIndex < 0)
		{
			// All three slots are still owned by the editor. Drop this frame
			// rather than blocking the render thread.
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			return;
		}

		ViewportFrameRing::Slot& Slot = m_EditorBridge->GetFrameRing()->GetSlot(static_cast<uint32_t>(SlotIndex));

		m_Renderer->BeginRenderToTexture(Slot.Resource.Get(), Slot.Rtv);
		// @TODO: DrawCalls from game simulation to renderer.
		if (GetSpecification().bEnabledImGui)
		{
			m_Renderer->RenderImGui();
		}
		m_Renderer->EndRenderToTexture();
		m_Renderer->WaitForGpu();
		m_EditorBridge->PublishFrame(static_cast<uint32_t>(SlotIndex));

		if (m_Specification.TargetFps > 0)
		{
			static auto NextFrame = std::chrono::steady_clock::now();
			const auto Now = std::chrono::steady_clock::now();
			if (NextFrame < Now)
			{
				NextFrame = Now;
			}
			NextFrame += std::chrono::microseconds(1000000 / m_Specification.TargetFps);
			if (NextFrame > Now)
			{
				std::this_thread::sleep_until(NextFrame);
			}
		}

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

void Application::Shutdown()
{
	if (!m_Initialized)
	{
		return;
	}

	NFD::Quit();

	m_EditorBridge.reset();
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
