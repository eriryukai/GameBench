// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

class Window;
class RendererFactory;

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

	RendererFactory* GetFactory() const { return m_RendererFactory.get(); }
protected:
	bool InitializeRHI();

private:
	bool bInitialize = false;
	std::unique_ptr<RendererFactory> m_RendererFactory;
};
