// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "OS/Window/WindowTypes.h"

namespace GLFWWindow
{
	void* Create(const WindowSpecification& Spec);
	void Destroy(void* WindowHandle);
	void PumpMessages(bool& bOutShouldClose);
}
