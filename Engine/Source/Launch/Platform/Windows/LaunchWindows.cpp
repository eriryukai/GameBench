// Copyright (c) CreationArt. All Rights Reserved.
#include "stdafx.h"

#ifdef GAMEBENCH_PLATFORM_WINDOWS

extern int GuardedMain(int argc, char** argv);

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPSTR lpCmdLine, _In_ int nCmdShow)
{
	(void)hInstance;
	(void)hPrevInstance;
	(void)lpCmdLine;
	(void)nCmdShow;

	if (IsDebuggerPresent())
	{
		// Debugger attached — let it catch the crash on the exact line.
		return GuardedMain(__argc, __argv);
	}

	int Result;
	__try
	{
		Result = GuardedMain(__argc, __argv);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		fprintf(stderr, "[LaunchWindows] FATAL: Unhandled exception in GuardedMain (code 0x%08X)\n", GetExceptionCode());
		Result = 1;
	}
	return Result;
}

#endif // GAMEBENCH_PLATFORM_WINDOWS
