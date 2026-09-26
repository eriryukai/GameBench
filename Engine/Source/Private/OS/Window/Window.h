// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "WindowTypes.h"

class Window
{
public:
	Window();
	~Window();

	bool Create(const WindowSpecification& Spec);
	void Destroy();

	void PumpMessages();
	bool ShouldClose() const;

	void PollFramebufferSize(uint32_t& OutWidth, uint32_t& OutHeight) const;
	void WaitEventsTimeout(double Seconds) const;

	void* GetNativeHandle() const;

	// Backend specific render target handle, i.e. an HWND on Windows. This is what the
	// renderer needs in order to create a swap chain, as opposed to GetNativeHandle()
	// which returns the backend's own window object.
	void* GetHandle() const;

	uint32_t GetWidth() const;
	uint32_t GetHeight() const;

private:
	void* m_NativeHandle = nullptr;
	bool m_bShouldClose = false;
	WindowSpecification m_Spec;
};
