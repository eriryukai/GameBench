// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Core/LayerStack.h"
#include "OS/Window/WindowTypes.h"

#include <cstdint>
#include <memory>
#include <string>

class EditorBridge;
class Renderer;
class Window;

struct ApplicationSpecification
{
	WindowSpecification Window;
	bool bEnabledImGui = false;
	bool VSync = true;

	// Editor mode is headless: no native window/swap chain. The renderer writes
	// into a three-slot shared texture ring and publishes it over the named pipe.
	bool bEditorMode = false;
	std::string EditorPipeName;
	uint32_t ViewportWidth = 1280;
	uint32_t ViewportHeight = 720;
	uint32_t TargetFps = 60;
	bool bUseAdapterLuid = false;
	uint64_t AdapterLuid = 0;
};

class Application
{
public:
	Application(const ApplicationSpecification& specification);
	virtual ~Application();

	int Initialize(void* PlatformData = nullptr);
	void Tick();
	void Shutdown();
	void Run(void* PlatformData = nullptr);

	void Close();

	void PushLayer(Layer* layer);
	void PushOverlay(Layer* layer);
	void PopLayer(Layer* layer);
	void PopOverlay(Layer* layer);

	bool IsInitialized() const { return m_Initialized; }
	bool IsRunning() const { return m_Running; }

	const ApplicationSpecification& GetSpecification() const { return m_Specification; }
	const std::string& GetLastError() const { return m_LastError; }

	// Null safe accessor, for code that may run before or after an application exists.
	static Application* GetInstance() { return s_Instance; }
	static Application& Get() { return *s_Instance; }

private:
	ApplicationSpecification m_Specification;
	LayerStack m_LayerStack;

	std::unique_ptr<Renderer> m_Renderer;
	std::unique_ptr<Window> m_Window;
	std::unique_ptr<EditorBridge> m_EditorBridge;

	std::string m_LastError;
	bool m_Initialized = false;
	bool m_Running = false;

	static Application* s_Instance;
};

// Implemented by the client
Application* CreateApplication(int argc, char** argv);
