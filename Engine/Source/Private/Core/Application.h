// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Core/LayerStack.h"
#include "OS/Window/WindowTypes.h"

#include <memory>
#include <string>

class Renderer;
class Window;

struct ApplicationSpecification
{
	WindowSpecification Window;
	bool VSync = true;
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

	std::string m_LastError;
	bool m_Initialized = false;
	bool m_Running = false;

	static Application* s_Instance;
};

// Implemented by the client
Application* CreateApplication(int argc, char** argv);
