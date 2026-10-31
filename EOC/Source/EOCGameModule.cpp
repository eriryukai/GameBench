// Copyright (c) CreationArt. All Rights Reserved.

#include "EOCApplication.h"

// The Engine owns WinMain and the runtime loop. The game module supplies its
// application here instead of defining a process entry point of its own.
Application* CreateApplication(int argc, char** argv)
{
	(void)argc;
	(void)argv;

	ApplicationSpecification Specification;
	Specification.Window.SetTitle("EOC");
	Specification.Window.SetWidth(1280);
	Specification.Window.SetHeight(720);
	Specification.Window.SetStartMaximized(true);
	Specification.Window.SetResizable(true);
	Specification.Window.SetDecorated(true);
	Specification.VSync = true;
	Specification.bEnabledImGui = true;

	return new EOCApplication(Specification);
}
