// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Core/Application.h"

#include <cstdlib>
#include <string>

#if !defined(MORTARENGINE_PLATFORM_WINDOWS)
#error "No platform defined. Define MORTARENGINE_PLATFORM_WINDOWS to 1 in PlatformConfig.h"
#endif

namespace
{
	bool HasArgument(int argc, char** argv, const char* Name)
	{
		for (int Index = 1; Index < argc; ++Index)
		{
			if (std::string(argv[Index]) == Name)
			{
				return true;
			}
		}
		return false;
	}

	std::string GetArgumentValue(int argc, char** argv, const std::string& Prefix)
	{
		for (int Index = 1; Index < argc; ++Index)
		{
			const std::string Argument = argv[Index];
			if (Argument.rfind(Prefix, 0) == 0)
			{
				return Argument.substr(Prefix.size());
			}
		}
		return {};
	}

	uint32_t GetArgumentUInt(int argc, char** argv, const std::string& Prefix, uint32_t DefaultValue)
	{
		const std::string Value = GetArgumentValue(argc, argv, Prefix);
		if (Value.empty())
		{
			return DefaultValue;
		}

		char* End = nullptr;
		const unsigned long Parsed = std::strtoul(Value.c_str(), &End, 10);
		return End != Value.c_str() ? static_cast<uint32_t>(Parsed) : DefaultValue;
	}
}

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
	ApplicationSpecification Specification;
	Specification.Window.SetTitle("MortarEngine");
	Specification.Window.SetWidth(1280);
	Specification.Window.SetHeight(720);
	Specification.Window.SetStartMaximized(true);
	Specification.Window.SetResizable(true);
	Specification.Window.SetDecorated(true);
	Specification.VSync = true;
	Specification.bEnabledImGui = true;

	if (HasArgument(argc, argv, "--editor"))
	{
		Specification.bEditorMode = true;
		Specification.VSync = false;
		Specification.EditorPipeName = GetArgumentValue(argc, argv, "--pipe=");
		Specification.ViewportWidth = GetArgumentUInt(argc, argv, "--width=", 1280);
		Specification.ViewportHeight = GetArgumentUInt(argc, argv, "--height=", 720);
		Specification.TargetFps = GetArgumentUInt(argc, argv, "--fps=", 60);
		const std::string AdapterLuidValue = GetArgumentValue(argc, argv, "--adapter-luid=");
		if (!AdapterLuidValue.empty())
		{
			char* End = nullptr;
			const unsigned long long Parsed = std::strtoull(AdapterLuidValue.c_str(), &End, 0);
			if (End != AdapterLuidValue.c_str())
			{
				Specification.AdapterLuid = static_cast<uint64_t>(Parsed);
				Specification.bUseAdapterLuid = Specification.AdapterLuid != 0;
			}
		}
	}

	return new Engine(Specification);
}
