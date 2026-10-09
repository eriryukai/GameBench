// Copyright (c) CreationArt. All Rights Reserved.
#include "stdafx.h"

#ifdef GAMEBENCH_PLATFORM_WINDOWS

#include "Core/Runtime.h"

#include <cstdio>
#include <iostream>
#include <io.h>
#include <fcntl.h>

#if !defined(GAMEBENCH_SHIPPING)
// The engine is linked as a WINDOWS subsystem app (entry point: WinMain), so it has
// no console by default. Attach to the parent console when launched from a terminal,
// otherwise allocate a fresh one, then rebind the standard streams so std::cout,
// std::cerr and std::print are visible. Shipping builds skip this entirely.
static void SetupConsole()
{
	if (!AttachConsole(ATTACH_PARENT_PROCESS) && !AllocConsole())
	{
		return;
	}

	FILE* Stream = nullptr;
	freopen_s(&Stream, "CONOUT$", "w", stdout);
	freopen_s(&Stream, "CONOUT$", "w", stderr);
	freopen_s(&Stream, "CONIN$", "r", stdin);

	std::ios::sync_with_stdio();

	SetConsoleOutputCP(CP_UTF8);
	SetConsoleTitleW(L"GameBench");
}
#endif

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPSTR lpCmdLine, _In_ int nCmdShow)
{
	(void)hPrevInstance;
	(void)lpCmdLine;
	(void)nCmdShow;

#if !defined(GAMEBENCH_SHIPPING)
	SetupConsole();
#endif

	void* PlatformData = static_cast<void*>(hInstance);

	// RunEngine owns a frame, so the SEH filter below does not have to coexist with C++
	// objects in this function.
	if (IsDebuggerPresent())
	{
		return RunEngine(__argc, __argv, PlatformData);
	}

	int Result;
	__try
	{
		Result = RunEngine(__argc, __argv, PlatformData);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		fprintf(stderr, "[LaunchWindows] FATAL: Unhandled exception in RunEngine (code 0x%08X)\n", GetExceptionCode());
		Result = 1;
	}
	return Result;
}

#endif // GAMEBENCH_PLATFORM_WINDOWS
