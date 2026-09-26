// Copyright (c) CreationArt. All Rights Reserved.
#include "stdafx.h"

#ifdef GAMEBENCH_PLATFORM_WINDOWS

#include "Core/Runtime.h"

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPSTR lpCmdLine, _In_ int nCmdShow)
{
	(void)hPrevInstance;
	(void)lpCmdLine;
	(void)nCmdShow;

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
