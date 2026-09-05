// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "EngineLoop.h"

#if !defined(MORTAR_PLATFORM_WINDOWS)
#error "No platform defined. Define MORTAR_PLATFORM_WINDOWS"
#endif

int GuardedMain(int argc, char** argv)
{
	int Result = GEngineLoop.PreInitialize(argc, argv);
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
	return 0;}
