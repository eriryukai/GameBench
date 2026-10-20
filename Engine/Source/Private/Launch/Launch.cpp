// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Core/Application.h"
#include "Launch/CommandLine.h"

#if !defined(GAMEBENCH_PLATFORM_WINDOWS)
#error "No platform defined. Define GAMEBENCH_PLATFORM_WINDOWS to 1 in PlatformConfig.h"
#endif

//@TODO: Figure out a model, similiar to unreal engine that even with a game project we still have one application. 
//       With editor, same BUT we will interop the function. Right now, We created 4 functions for interop.
//       Ideally, we just have ONE interop function and stuff like tick, shutdown etx just handle by editor telling the engine to shut down or tick like giving messages
//@TODO: Figure out a design. Using UnrealEngine as inspiration, the Engine class acts as the base Application class, and depends on Runtime or Editor, we will have 
//       EditorEngine or GameEngine
class Engine : public Application
{
public:
	explicit Engine(const ApplicationSpecification& Specification)
		: Application(Specification)
	{
	}
};

Application* CreateApplication(int argc, char** argv)
{
	FCommandLineOptions Options;
	std::string CommandLineError;
	if (!ParseCommandLine(argc, argv, Options, CommandLineError))
	{
		std::print(stderr, "[GameBench] {}\n", CommandLineError);
		return nullptr;
	}

	const bool bEditorMode = Options.Mode == EApplicationMode::Editor;

	ApplicationSpecification Specification;
	Specification.Mode = Options.Mode;
	Specification.IpcPipeName = Options.IpcPipeName;
	Specification.Window.SetTitle(bEditorMode ? "GameBench Engine (Editor)" : "GameBench");
	Specification.Window.SetWidth(1280);
	Specification.Window.SetHeight(720);
	Specification.Window.SetStartMaximized(true);
	Specification.Window.SetResizable(true);
	Specification.Window.SetDecorated(true);
	Specification.Window.SetVisible(Options.bShowWindow);
	Specification.VSync = true;
	Specification.bEnabledImGui = true;

	return new Engine(Specification);
}
