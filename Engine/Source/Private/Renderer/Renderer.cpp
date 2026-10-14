// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "Renderer/RendererFactory.h"
#include "Renderer/ImGui/ImGuiRenderer.h"
#include "OS/Window/Window.h"

Renderer::Renderer()
{
}

Renderer::~Renderer()
{
	UnInitialize();
}

void Renderer::Initialize(Window* window)
{
	if (IsInitialized())
	{
		return;
	}

	m_RendererFactory = std::make_unique<RendererFactory>();
	m_RendererFactory->SetWindow(window);

	if (!InitializeRHI())
	{
		m_RendererFactory->UnInitialize();
		m_RendererFactory.reset();
		bInitialize = false;
		return;
	}

	// The overlay borrows the window and the RHI's device/queue. It owns its own SRV heap.
	if (window)
	{
		ImGuiSpecification ImGuiSpec;
		ImGuiSpec.bEnabledKeyboardControls = true;
		ImGuiSpec.bShowDemoWindow = true;
		ImGuiSpec.WindowHandle = window->GetNativeHandle();
		ImGuiSpec.Device = m_RendererFactory->GetDevice();
		ImGuiSpec.CommandQueue = m_RendererFactory->GetCommandQueue();
		ImGuiSpec.NumFramesInFlight = m_RendererFactory->GetFramesInFlight();
		ImGuiSpec.RTVFormat = m_RendererFactory->GetRTVFormat();
		ImGuiSpec.DSVFormat = DXGI_FORMAT_UNKNOWN;

		m_ImGuiRenderer = std::make_unique<ImGuiRenderer>();
		if (!m_ImGuiRenderer->Initialize(ImGuiSpec))
		{
			std::print("[Renderer] Failed to initialize the ImGui overlay.\n");
			m_ImGuiRenderer.reset();
		}
	}

	bInitialize = true;
}

void Renderer::UnInitialize()
{
	if (!IsInitialized())
	{
		return;
	}

	// Tear down the overlay before the RHI resources it borrows (device, heap, window).
	m_ImGuiRenderer.reset();

	if (m_RendererFactory)
	{
		m_RendererFactory->UnInitialize();
		m_RendererFactory.reset();
	}

	bInitialize = false;
}

void Renderer::BeginRender()
{
	if (m_RendererFactory)
	{
		m_RendererFactory->BeginFrame();
	}
}

void Renderer::EndRender()
{
	if (m_RendererFactory)
	{
		m_RendererFactory->EndFrame();
	}
}

void Renderer::Present()
{
	if (m_RendererFactory)
	{
		m_RendererFactory->PresentFrame();
	}
}

void Renderer::RenderImGui()
{
	if (!m_ImGuiRenderer || !m_ImGuiRenderer->IsInitialized() || !m_RendererFactory)
	{
		return;
	}

	m_ImGuiRenderer->BeginFrame();
	m_ImGuiRenderer->Render();
	m_ImGuiRenderer->EndFrame(m_RendererFactory->GetCommandList());
}

bool Renderer::InitializeRHI()
{
	m_RendererFactory->Initialize();
	return m_RendererFactory->IsRHIValid();
}
