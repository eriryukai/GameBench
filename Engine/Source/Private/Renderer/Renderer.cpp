// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "Renderer/RendererFactory.h"

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

	bInitialize = true;
}

void Renderer::UnInitialize()
{
	if (!IsInitialized())
	{
		return;
	}

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

bool Renderer::InitializeRHI()
{
	m_RendererFactory->Initialize();
	return m_RendererFactory->IsRHIValid();
}
