// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Renderer/Viewport/ViewportInput.h"

#include <d3d12.h>

#include <cstdint>
#include <memory>
#include <vector>

class Window;
class RendererFactory;
class ImGuiRenderer;

class Renderer
{
public:
	Renderer();
	~Renderer();

	void Initialize(Window* window, bool bEnableImGui, uint32_t ViewportWidth, uint32_t ViewportHeight, const LUID* AdapterLuid = nullptr);
	void UnInitialize();

	bool IsInitialized() const { return bInitialize; }

	// Frame loop.
	void BeginRender();
	void EndRender();
	void Present();

	// Editor viewport output.
	void BeginRenderToTexture(ID3D12Resource* Target, D3D12_CPU_DESCRIPTOR_HANDLE Rtv);
	void EndRenderToTexture();
	void WaitForGpu();

	void RenderImGui();
	void ApplyViewportInput(const std::vector<ViewportInputEvent>& Events);

	RendererFactory* GetFactory() const { return m_RendererFactory.get(); }
	ImGuiRenderer* GetImGuiRenderer() const { return m_ImGuiRenderer.get(); }
protected:
	bool InitializeRHI(const LUID* AdapterLuid);

private:
	bool bInitialize = false;
	std::unique_ptr<RendererFactory> m_RendererFactory;
	std::unique_ptr<ImGuiRenderer> m_ImGuiRenderer;
};
