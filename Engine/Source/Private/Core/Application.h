// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Core/LayerStack.h"
#include "InteropProtocol.h"
#include "OS/Window/WindowTypes.h"

#include <memory>
#include <string>

class InteropNamePipeClient;
class Renderer;
class Window;

// How this Engine.exe instance was launched:
//   Game   - standalone game run (Engine.exe [--game])
//   Editor - hosted by Editor.exe (Engine.exe --editor --ipc-pipe=<name>)
enum class EApplicationMode
{
	Game,
	Editor,
};

struct ApplicationSpecification
{
	WindowSpecification Window;
	bool bEnabledImGui = false;
	bool VSync = true;

	EApplicationMode Mode = EApplicationMode::Game;

	// Required when Mode == Editor; connects back to the editor's pipe server.
	std::string IpcPipeName;
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
	bool IsEditorMode() const { return m_Specification.Mode == EApplicationMode::Editor; }

	const ApplicationSpecification& GetSpecification() const { return m_Specification; }
	const std::string& GetLastError() const { return m_LastError; }

	// Null safe accessor, for code that may run before or after an application exists.
	static Application* GetInstance() { return s_Instance; }
	static Application& Get() { return *s_Instance; }

private:
	void ReportProgress(const std::string& Step, const std::string& Message, int32_t Index, int32_t Total);
	void DrainInteropMessages();

	ApplicationSpecification m_Specification;
	LayerStack m_LayerStack;

	std::unique_ptr<Renderer> m_Renderer;
	std::unique_ptr<Window> m_Window;
	std::unique_ptr<InteropNamePipeClient> m_IpcClient;

	std::string m_LastError;
	bool m_Initialized = false;
	bool m_Running = false;

	static Application* s_Instance;
};

// Implemented by the client
Application* CreateApplication(int argc, char** argv);
