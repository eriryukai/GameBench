// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Runtime.h"
#include "Application.h"

int RunEngine(int argc, char** argv, void* PlatformData)
{
	Application* App = CreateApplication(argc, argv);
	if (!App)
	{
		std::print("RunEngine: CreateApplication returned nothing.");
		return 1;
	}

	if (App->Initialize(PlatformData) != 0)
	{
		std::print("RunEngine: {}", App->GetLastError());
		delete App;
		return 1;
	}

	App->Run();

	delete App;
	return 0;
}
