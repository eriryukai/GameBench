// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "EngineLoop.h"

<<<<<<< HEAD
#if !defined(GAMEBENCH_PLATFORM_WINDOWS)
#error "No platform defined. Define GAMEBENCH_PLATFORM_WINDOWS"
=======
#if !MORTAR_PLATFORM_WINDOWS
#error "No platform defined. Set MORTAR_PLATFORM_WINDOWS to 1 in PlatformConfig.h"
>>>>>>> 376238c ([*/+]Add GLFW as the platform agnostic OS Windowing backend)
#endif

int GuardedMain(int argc, char** argv, void* PlatformData)
{
	int Result = GEngineLoop.PreInitialize(argc, argv, PlatformData);
	if (Result != 0)
	{
		GEngineLoop.Exit();
		return Result;
	}

	Result = GEngineLoop.Initialize();
	if (Result != 0)
	{
		GEngineLoop.Exit();
		return Result;
	}

	while (!GEngineLoop.IsExitRequested())
	{
		GEngineLoop.Tick();
	}

	GEngineLoop.Exit();
	return 0;
}
