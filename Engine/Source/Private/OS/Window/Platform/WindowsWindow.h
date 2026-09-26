// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "OS/Window/WindowTypes.h"

namespace WindowsWindow
{
	void* Create(const WindowSpecification& Spec);
	void Destroy(HWND WindowHandle);
	void PumpMessages(bool& bOutShouldClose);
	void PollFramebufferSize(void* WindowHandle, uint32_t& OutWidth, uint32_t& OutHeight);
	void WaitEventsTimeout(double Seconds);
}
