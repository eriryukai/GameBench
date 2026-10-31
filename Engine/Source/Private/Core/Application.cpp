// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Core/Application.h"
#include "OS/Window/Window.h"
#include "Renderer/Renderer.h"
#include "Renderer/RendererFactory.h"

#include <nfd.hpp>

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
	m_Renderer->Initialize(m_Window.get());
	if (!m_Renderer->IsInitialized())
	{
		m_LastError = "Could not initialize the renderer.";
		m_Renderer.reset();
		m_Window.reset();
		return 1;
	}

	m_Renderer->GetFactory()->SetVSyncEnabled(m_Specification.VSync);

	if (NFD::Init() != NFD_OKAY)
	{
		m_LastError = "Could not initialize file dialogs.";
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

	NFD::Quit();

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
