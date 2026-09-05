#pragma once

#include "OS/Window/WindowTypes.h"

namespace WindowsWindow
{
	void* Create(const WindowSpecification& Spec);
	void Destroy(HWND WindowHandle);
	void PumpMessages(bool& bOutShouldClose);
}
