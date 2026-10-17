// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <memory>

class Window;
class RendererFactory;
class ImGuiRenderer;

class Renderer
{
public:
	Renderer();
	~Renderer();

	void Initialize(Window* window);
	void UnInitialize();

	bool IsInitialized() const { return bInitialize; }

	// Frame loop.
	void BeginRender();
	void EndRender();
	void Present();

	void RenderImGui();

	RendererFactory* GetFactory() const { return m_RendererFactory.get(); }
	ImGuiRenderer* GetImGuiRenderer() const { return m_ImGuiRenderer.get(); }
protected:
	bool InitializeRHI();

private:
	bool bInitialize = false;
	std::unique_ptr<RendererFactory> m_RendererFactory;
	std::unique_ptr<ImGuiRenderer> m_ImGuiRenderer;
};
