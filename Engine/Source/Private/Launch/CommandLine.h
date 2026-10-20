// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <string>

#include "Core/Application.h"

// Parsed --editor / --game / --ipc-pipe / --show-window contract.
struct FCommandLineOptions
{
	EApplicationMode Mode = EApplicationMode::Game;
	bool bShowWindow = true;
	std::string IpcPipeName;
};

// Parses the engine command line. Unknown flags are an error: the engine never
// guesses what the editor meant. Returns false and fills OutError on failure.
bool ParseCommandLine(int argc, char** argv, FCommandLineOptions& OutOptions, std::string& OutError);
