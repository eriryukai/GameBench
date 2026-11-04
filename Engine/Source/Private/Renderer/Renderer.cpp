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

void Renderer::Initialize(Window* window, bool bEnableImGui, uint32_t ViewportWidth, uint32_t ViewportHeight, const LUID* AdapterLuid)
{
	if (IsInitialized())
	{
		return;
	}

	m_RendererFactory = std::make_unique<RendererFactory>();
	m_RendererFactory->SetWindow(window);

	const bool bRhi = InitializeRHI(AdapterLuid);
	if (!bRhi || (window && !m_RendererFactory->HasSwapChain()))
	{
		m_RendererFactory->UnInitialize();
		m_RendererFactory.reset();
		bInitialize = false;
		return;
	}

	// The overlay borrows the RHI's device/queue and, in windowed mode, the
	// native window. Headless/editor mode skips the GLFW backend and feeds ImGui
	// the viewport size directly.
	if (bEnableImGui)
	{
		ImGuiSpecification ImGuiSpec;
		ImGuiSpec.bEnabledKeyboardControls = true;
		ImGuiSpec.bShowDemoWindow = true;
		ImGuiSpec.WindowHandle = window ? window->GetNativeHandle() : nullptr;
		ImGuiSpec.ViewportWidth = ViewportWidth;
		ImGuiSpec.ViewportHeight = ViewportHeight;
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
		else
		{
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

void Renderer::BeginRenderToTexture(ID3D12Resource* Target, D3D12_CPU_DESCRIPTOR_HANDLE Rtv)
{
	if (m_RendererFactory)
	{
		m_RendererFactory->BeginFrameToTexture(Target, Rtv);
	}
}

void Renderer::EndRenderToTexture()
{
	if (m_RendererFactory)
	{
		m_RendererFactory->EndFrameToTexture();
	}
}

void Renderer::WaitForGpu()
{
	if (m_RendererFactory)
	{
		m_RendererFactory->WaitForGpu();
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

bool Renderer::InitializeRHI(const LUID* AdapterLuid)
{
	m_RendererFactory->Initialize(AdapterLuid);
	return m_RendererFactory->IsRHIValid();
}
