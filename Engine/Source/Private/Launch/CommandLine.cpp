// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "CommandLine.h"

#include <string_view>

bool ParseCommandLine(int argc, char** argv, FCommandLineOptions& OutOptions, std::string& OutError)
{
	constexpr std::string_view IpcPipeFlag = "--ipc-pipe=";
	bool bShowWindowExplicit = false;

	for (int Index = 1; Index < argc; ++Index)
	{
		const std::string_view Argument = argv[Index];

		if (Argument == "--editor")
		{
			OutOptions.Mode = EApplicationMode::Editor;
		}
		else if (Argument == "--game")
		{
			OutOptions.Mode = EApplicationMode::Game;
		}
		else if (Argument == "--show-window")
		{
			// Only meaningful in editor mode; the game always shows its window.
			bShowWindowExplicit = true;
			OutOptions.bShowWindow = true;
		}
		else if (Argument.starts_with(IpcPipeFlag))
		{
			OutOptions.IpcPipeName = std::string(Argument.substr(IpcPipeFlag.size()));
		}
		else
		{
			OutError = "Unknown command line argument: " + std::string(Argument);
			return false;
		}
	}

	if (OutOptions.Mode == EApplicationMode::Editor)
	{
		if (OutOptions.IpcPipeName.empty())
		{
			OutError = "--editor requires --ipc-pipe=<name>; the engine never guesses the editor pipe.";
			return false;
		}

		// The editor draws the viewport; unless asked otherwise the engine stays headless.
		if (!bShowWindowExplicit)
		{
			OutOptions.bShowWindow = false;
		}
	}
	else if (!OutOptions.IpcPipeName.empty())
	{
		OutError = "--ipc-pipe is only valid together with --editor.";
		return false;
	}

	return true;
}
